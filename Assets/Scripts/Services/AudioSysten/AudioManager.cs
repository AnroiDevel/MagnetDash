using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// IAudioService + управление AudioMixer (Master/SFX/UI/Music), линейные уровни 0..1, мьют, снапшоты.
/// Пул AudioSource для one-shot; лупы через IAudioHandle с fade.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour, IAudioService
{
    // ---------- Pool ----------
    [Header("Pool")]
    [SerializeField] private int _poolSize = 16;

    [Header("Routing (optional default)")]
    [SerializeField] private AudioMixerGroup _defaultOutput;

    // ---------- Mixer Control ----------
    [Header("Mixer Control")]
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private string _paramMaster = "MasterVolume";
    [SerializeField] private string _paramSfx = "SFXVolume";
    [SerializeField] private string _paramUi = "UIVolume";
    [SerializeField] private string _paramMusic = "MusicVolume";

    [SerializeField, Range(0f, 1f)] private float _volMaster = 1f;
    [SerializeField, Range(0f, 1f)] private float _volSfx = 1f;
    [SerializeField, Range(0f, 1f)] private float _volUi = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _volMusic = 0.8f;

    [SerializeField] private bool _muted = false;

    [Header("Snapshots (optional)")]
    [SerializeField] private AudioMixerSnapshot _snapshotGameplay;
    [SerializeField] private AudioMixerSnapshot _snapshotPaused;
    [SerializeField] private float _snapshotFade = 0.12f;

    // ---------- Internal ----------
    private readonly Queue<AudioSource> _pool = new();
    private readonly Dictionary<string, float> _cooldowns = new();   // key -> nextTime
    private readonly Dictionary<SfxEvent, int> _concurrency = new(); // event -> active

    // Prefs keys
    private const string _kPrefMaster = "audio.master";
    private const string _kPrefSfx = "audio.sfx";
    private const string _kPrefUi = "audio.ui";
    private const string _kPrefMusic = "audio.music";
    private const string _kPrefMuted = "audio.muted";

    private void Awake()
    {
        Audio.Initialize(this);

        // Init pool
        int size = Mathf.Max(1, _poolSize);
        for(int i = 0; i < size; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D по умолчанию
            if(_defaultOutput != null)
                src.outputAudioMixerGroup = _defaultOutput;
            _pool.Enqueue(src);
        }

        // Load volumes/mute
        LoadPrefs();
        ApplyAllToMixer();

        // Стартовый снапшот (если задан)
        if(_snapshotGameplay != null)
            _snapshotGameplay.TransitionTo(_snapshotFade);
    }

    // ===================== IAudioService =====================

    public void Play(SfxEvent sfx, string key = null)
    {
        if(sfx == null)
            return;
        if(!PassCooldownAndConcurrency(sfx, key))
            return;

        AudioClip clip = sfx.RandomClip();
        if(clip == null)
            return;

        float vol = 1f, pitch = 1f;
        sfx.Randomize(ref vol, ref pitch);

        AudioSource src = Pop();
        SetupSource2D(src, sfx, vol, pitch);
        src.PlayOneShot(clip);

        RegisterPlay(sfx, key, clip.length / Mathf.Max(0.01f, pitch));
    }

    public void PlayAt(SfxEvent sfx, Vector3 worldPos, float spatial = 1f, float minDist = 2f, float maxDist = 20f, string key = null)
    {
        if(sfx == null)
            return;
        if(!PassCooldownAndConcurrency(sfx, key))
            return;

        AudioClip clip = sfx.RandomClip();
        if(clip == null)
            return;

        float vol = 1f, pitch = 1f;
        sfx.Randomize(ref vol, ref pitch);

        AudioSource src = Pop();
        SetupSource3D(src, sfx, vol, pitch, spatial, minDist, maxDist, worldPos);
        src.PlayOneShot(clip);

        RegisterPlay(sfx, key, clip.length / Mathf.Max(0.01f, pitch));
    }

    public IAudioHandle PlayLoop(SfxEvent sfx, Vector3 worldPos, float spatial = 1f, float minDist = 2f, float maxDist = 20f)
    {
        if(sfx == null)
            return null;

        AudioClip clip = sfx.RandomClip();
        if(clip == null)
            return null;

        float vol = 1f, pitch = 1f;
        sfx.Randomize(ref vol, ref pitch);

        GameObject go = new($"Loop_{sfx.name}");
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = vol;
        src.pitch = pitch;
        src.spatialBlend = Mathf.Clamp01(spatial);
        src.minDistance = minDist;
        src.maxDistance = maxDist;
        if(sfx.output != null)
            src.outputAudioMixerGroup = sfx.output;
        else if(_defaultOutput != null)
            src.outputAudioMixerGroup = _defaultOutput;

        src.Play();
        return new LoopHandle(this, src);
    }

    // ===================== Mixer API (Settings) =====================

    public void SetMasterVolume01(float value) { _volMaster = Mathf.Clamp01(value); ApplyMaster(); SavePrefs(); }
    public void SetSfxVolume01(float value) { _volSfx = Mathf.Clamp01(value); ApplySfx(); SavePrefs(); }
    public void SetUiVolume01(float value) { _volUi = Mathf.Clamp01(value); ApplyUi(); SavePrefs(); }
    public void SetMusicVolume01(float value) { _volMusic = Mathf.Clamp01(value); ApplyMusic(); SavePrefs(); }

    public float GetMasterVolume01() { return _volMaster; }
    public float GetSfxVolume01() { return _volSfx; }
    public float GetUiVolume01() { return _volUi; }
    public float GetMusicVolume01() { return _volMusic; }

    public void SetMuted(bool muted) { _muted = muted; ApplyAllToMixer(); SavePrefs(); }
    public bool GetMuted() { return _muted; }

    public void SetPausedSnapshot(bool paused)
    {
        if(_mixer == null)
            return;
        if(paused && _snapshotPaused != null)
            _snapshotPaused.TransitionTo(_snapshotFade);
        else if(!paused && _snapshotGameplay != null)
            _snapshotGameplay.TransitionTo(_snapshotFade);
    }

    // ===================== Internals =====================

    private AudioSource Pop()
    {
        AudioSource src = _pool.Dequeue();
        _pool.Enqueue(src);
        return src;
    }

    private static void SetupSource2D(AudioSource src, SfxEvent sfx, float vol, float pitch)
    {
        src.transform.localPosition = Vector3.zero;
        src.spatialBlend = 0f;
        if(sfx.output != null)
            src.outputAudioMixerGroup = sfx.output;
        src.volume = vol;
        src.pitch = pitch;
        src.minDistance = 1f;
        src.maxDistance = 100f;
    }

    private static void SetupSource3D(AudioSource src, SfxEvent sfx, float vol, float pitch, float spatial, float minDist, float maxDist, Vector3 pos)
    {
        src.transform.position = pos;
        src.spatialBlend = Mathf.Clamp01(spatial);
        if(sfx.output != null)
            src.outputAudioMixerGroup = sfx.output;
        src.volume = vol;
        src.pitch = pitch;
        src.minDistance = minDist;
        src.maxDistance = maxDist;
    }

    private bool PassCooldownAndConcurrency(SfxEvent sfx, string key)
    {
        if(!string.IsNullOrEmpty(key))
        {
            if(_cooldowns.TryGetValue(key, out float until) && Time.time < until)
                return false;
        }

        if(sfx.maxSimultaneous > 0)
        {
            _concurrency.TryGetValue(sfx, out int cnt);
            if(cnt >= sfx.maxSimultaneous)
                return false;
        }
        return true;
    }

    private void RegisterPlay(SfxEvent sfx, string key, float estSeconds)
    {
        if(!string.IsNullOrEmpty(key) && sfx.cooldown > 0f)
            _cooldowns[key] = Time.time + sfx.cooldown;

        if(sfx.maxSimultaneous > 0)
        {
            _concurrency.TryGetValue(sfx, out int cnt);
            _concurrency[sfx] = cnt + 1;
            StartCoroutine(ReleaseAfter(estSeconds, sfx));
        }
    }

    private IEnumerator ReleaseAfter(float t, SfxEvent sfx)
    {
        yield return new WaitForSeconds(t);
        if(_concurrency.TryGetValue(sfx, out int cnt))
            _concurrency[sfx] = Mathf.Max(0, cnt - 1);
    }

    // ---------- Mixer apply/save/load ----------

    private void ApplyAllToMixer()
    {
        ApplyParam(_paramMaster, _muted ? 0f : _volMaster);
        ApplyParam(_paramSfx, _muted ? 0f : _volSfx);
        ApplyParam(_paramUi, _muted ? 0f : _volUi);
        ApplyParam(_paramMusic, _muted ? 0f : _volMusic);
    }

    private void ApplyMaster() { ApplyParam(_paramMaster, _muted ? 0f : _volMaster); }
    private void ApplySfx() { ApplyParam(_paramSfx, _muted ? 0f : _volSfx); }
    private void ApplyUi() { ApplyParam(_paramUi, _muted ? 0f : _volUi); }
    private void ApplyMusic() { ApplyParam(_paramMusic, _muted ? 0f : _volMusic); }

    private void ApplyParam(string param, float linear01)
    {
        if(_mixer == null || string.IsNullOrEmpty(param))
            return;
        float dB = Linear01ToDb(linear01);
        _mixer.SetFloat(param, dB);
    }

    private float Linear01ToDb(float v)
    {
        // 0 -> -80 dB (почти тишина), 1 -> 0 dB
        float x = Mathf.Clamp01(v);
        return (x <= 0.0001f) ? -80f : 20f * Mathf.Log10(x);
    }

    private void SavePrefs()
    {
        PlayerPrefs.SetFloat(_kPrefMaster, _volMaster);
        PlayerPrefs.SetFloat(_kPrefSfx, _volSfx);
        PlayerPrefs.SetFloat(_kPrefUi, _volUi);
        PlayerPrefs.SetFloat(_kPrefMusic, _volMusic);
        PlayerPrefs.SetInt(_kPrefMuted, _muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadPrefs()
    {
        if(PlayerPrefs.HasKey(_kPrefMaster))
            _volMaster = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefMaster, _volMaster));
        if(PlayerPrefs.HasKey(_kPrefSfx))
            _volSfx = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefSfx, _volSfx));
        if(PlayerPrefs.HasKey(_kPrefUi))
            _volUi = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefUi, _volUi));
        if(PlayerPrefs.HasKey(_kPrefMusic))
            _volMusic = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefMusic, _volMusic));
        if(PlayerPrefs.HasKey(_kPrefMuted))
            _muted = PlayerPrefs.GetInt(_kPrefMuted, 0) != 0;
    }
}
