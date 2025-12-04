using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicManager : MonoBehaviour
{
    [Header("Lifecycle")]
    [SerializeField] private bool _persistAcrossScenes = true;

    [Header("Defaults")]
    [SerializeField, Range(0f, 5f)] private float _defaultFade = 0.6f;

    private AudioSource _a;
    private AudioSource _b;
    private bool _aActive;         // кто сейчас «основной»
    private Coroutine _xfadeCo;

    private void Awake()
    {
        // Два внутренних источника
        _a = CreateChildSource("MusicA");
        _b = CreateChildSource("MusicB");
        _a.volume = 0f;
        _b.volume = 0f;

        if(_persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private AudioSource CreateChildSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;          // луп включаем только на основном клипе
        s.spatialBlend = 0f;     // музыка — 2D
        return s;
    }

    public void Play(MusicTrack track, float fade = -1f)
    {
        if(track == null || track.Loop == null)
            return;
        if(fade < 0f)
            fade = _defaultFade;

        var from = _aActive ? _a : _b;
        var to = _aActive ? _b : _a;

        // Настройка to
        to.Stop();
        to.outputAudioMixerGroup = track.MusicBus != null ? track.MusicBus : to.outputAudioMixerGroup;
        to.volume = 0f;

        // Стартуем intro или сразу loop
        if(track.Intro != null)
        {
            to.clip = track.Intro;
            to.loop = false;
            to.Play();
            // Переключимся на луп после интро
            StartCoroutine(SwapToLoopAfter(to, track));
        }
        else
        {
            to.clip = track.Loop;
            to.loop = true;
            to.Play();
        }

        // Кроссфейд
        if(_xfadeCo != null)
            StopCoroutine(_xfadeCo);
        _xfadeCo = StartCoroutine(Crossfade(from, to, fade, track.Volume));

        _aActive = (to == _a);
    }

    public void Stop(float fade = -1f)
    {
        if(fade < 0f)
            fade = _defaultFade;
        if(_xfadeCo != null)
            StopCoroutine(_xfadeCo);
        StartCoroutine(FadeOutAndStop(_a, fade));
        StartCoroutine(FadeOutAndStop(_b, fade));
    }

    public void Pause() { _a.Pause(); _b.Pause(); }
    public void Resume() { _a.UnPause(); _b.UnPause(); }

    private IEnumerator SwapToLoopAfter(AudioSource src, MusicTrack track)
    {
        var intro = track.Intro;
        if(intro == null)
            yield break;
        // Ждём реальную длительность интро
        yield return new WaitForSecondsRealtime(intro.length);
        // Переключаемся на луп
        if(src != null && track.Loop != null)
        {
            src.clip = track.Loop;
            src.loop = true;
            src.Play();
        }
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float time, float targetVol)
    {
        float t = 0f;
        float startFrom = from != null ? from.volume : 0f;
        targetVol = Mathf.Clamp01(targetVol);

        while(t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, time));
            if(from != null)
                from.volume = Mathf.Lerp(startFrom, 0f, k);
            if(to != null)
                to.volume = Mathf.Lerp(0f, targetVol, k);
            yield return null;
        }

        if(from != null)
        {
            from.volume = 0f;
            from.Stop();
        }
        if(to != null)
            to.volume = targetVol;
    }

    private IEnumerator FadeOutAndStop(AudioSource s, float time)
    {
        if(s == null || !s.isPlaying)
            yield break;
        float start = s.volume, t = 0f;
        while(t < time)
        {
            t += Time.unscaledDeltaTime;
            s.volume = Mathf.Lerp(start, 0f, t / Mathf.Max(0.0001f, time));
            yield return null;
        }
        s.Stop();
        s.volume = 0f;
    }
}
