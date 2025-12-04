using UnityEngine;

public static class Audio
{
    private static IAudioService _cachedService;
    private static bool _triedResolve;

    private static IAudioService Service
    {
        get
        {
            if(_cachedService != null)
                return _cachedService;

            if(_triedResolve)
                return null;

            _triedResolve = true;

            if(ServiceLocator.TryGet<IAudioService>(out var s))
            {
                _cachedService = s;
                return s;
            }

#if UNITY_EDITOR
            Debug.LogWarning("[Audio] IAudioService not found in ServiceLocator.");
#endif
            return null;
        }
    }

    public static void Play(SfxEvent sfx, string key = null)
    {
        var s = Service;
        if(s == null || sfx == null)
            return;

        s.Play(sfx, key);
    }

    public static void PlayAt(
        SfxEvent sfx,
        Vector3 worldPos,
        float spatial = 1f,
        float minDist = 2f,
        float maxDist = 20f,
        string key = null
    )
    {
        var s = Service;
        if(s == null || sfx == null)
            return;

        s.PlayAt(sfx, worldPos, spatial, minDist, maxDist, key);
    }

    public static IAudioHandle PlayLoop(
        SfxEvent sfx,
        Vector3 worldPos,
        float spatial = 1f,
        float minDist = 2f,
        float maxDist = 20f
    )
    {
        var s = Service;
        if(s == null || sfx == null)
            return null;

        return s.PlayLoop(sfx, worldPos, spatial, minDist, maxDist);
    }

    public static void ResetCache()
    {
        _cachedService = null;
        _triedResolve = false;
    }
}
