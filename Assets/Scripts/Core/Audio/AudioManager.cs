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

    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.8f;

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
        ServiceLocator.Register<IAudioService>(this);

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


    public float MusicVolume01
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            ApplyAllToMixer();
            SavePrefs();
        }
    }

    public float SfxVolume01
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp01(value);
            ApplyAllToMixer();
            SavePrefs();
        }
    }

    public float MasterVolume01
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Mathf.Clamp01(value);
            ApplyAllToMixer();
            SavePrefs();
        }
    }

    public float UIVolume01
    {
        get => _uiVolume;
        set
        {
            _uiVolume = Mathf.Clamp01(value);
            ApplyAllToMixer();
            SavePrefs();
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            ApplyAllToMixer();
            SavePrefs();
        }
    }

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
        ApplyParam(_paramMaster, _muted ? 0f : _masterVolume);
        ApplyParam(_paramSfx, _muted ? 0f : _sfxVolume);
        ApplyParam(_paramUi, _muted ? 0f : _uiVolume);
        ApplyParam(_paramMusic, _muted ? 0f : _musicVolume);
    }


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
        PlayerPrefs.SetFloat(_kPrefMaster, _masterVolume);
        PlayerPrefs.SetFloat(_kPrefSfx, _sfxVolume);
        PlayerPrefs.SetFloat(_kPrefUi, _uiVolume);
        PlayerPrefs.SetFloat(_kPrefMusic, _musicVolume);
        PlayerPrefs.SetInt(_kPrefMuted, _muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadPrefs()
    {
        if(PlayerPrefs.HasKey(_kPrefMaster))
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefMaster, _masterVolume));
        if(PlayerPrefs.HasKey(_kPrefSfx))
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefSfx, _sfxVolume));
        if(PlayerPrefs.HasKey(_kPrefUi))
            _uiVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefUi, _uiVolume));
        if(PlayerPrefs.HasKey(_kPrefMusic))
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(_kPrefMusic, _musicVolume));
        if(PlayerPrefs.HasKey(_kPrefMuted))
            _muted = PlayerPrefs.GetInt(_kPrefMuted, 0) != 0;
    }
}
