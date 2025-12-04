using System.Collections;
using UnityEngine;

/// <summary>
/// ’эндл дл€ лупов с плавным фейдом и корректным Stop.
/// </summary>
public sealed class LoopHandle : IAudioHandle
{
    private readonly AudioManager _owner;
    private readonly AudioSource _src;

    public LoopHandle(AudioManager owner, AudioSource src)
    {
        _owner = owner;
        _src = src;
    }

    public bool IsValid { get { return _src != null; } }

    public void SetVolume(float v, float fadeSeconds = 0f)
    {
        if(_src == null)
            return;
        if(fadeSeconds <= 0f)
        { _src.volume = v; return; }
        _owner.StartCoroutine(FadeVolume(_src, v, fadeSeconds));
    }

    public void Stop(float fadeSeconds = 0.1f)
    {
        if(_src == null)
            return;
        if(fadeSeconds <= 0f)
        {
            Object.Destroy(_src.gameObject);
            return;
        }
        _owner.StartCoroutine(FadeAndDestroy(_src, fadeSeconds));
    }

    private IEnumerator FadeVolume(AudioSource src, float to, float duration)
    {
        float from = src.volume;
        float t = 0f;
        while(t < 1f && src != null)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            src.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
    }

    private IEnumerator FadeAndDestroy(AudioSource src, float duration)
    {
        float from = src.volume;
        float t = 0f;
        while(t < 1f && src != null)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            src.volume = Mathf.Lerp(from, 0f, t);
            yield return null;
        }
        if(src != null)
            Object.Destroy(src.gameObject);
    }
}
