using System;
using UnityEngine;

public static class Audio
{
    private static IAudioService _service;
    private static bool _hooked;

    static Audio()
    {
        HookEvents();
        TryInitialResolve();
    }

    private static void HookEvents()
    {
        if(_hooked)
            return;

        _hooked = true;

        ServiceLocator.Registered += OnServiceRegistered;
        ServiceLocator.Unregistered += OnServiceUnregistered;
    }

    private static void OnServiceRegistered(Type type, object instance)
    {
        if(type == typeof(IAudioService))
            _service = instance as IAudioService;
    }

    private static void OnServiceUnregistered(Type type)
    {
        if(type == typeof(IAudioService))
            _service = null;
    }

    private static void TryInitialResolve()
    {
        if(_service != null)
            return;

        if(ServiceLocator.TryGet<IAudioService>(out var s))
            _service = s;
    }

    private static IAudioService Service
    {
        get
        {
            if(_service == null)
                TryInitialResolve();
            return _service;
        }
    }

    // ============ API ============

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
        string key = null)
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
        float maxDist = 20f)
    {
        var s = Service;
        if(s == null || sfx == null)
            return null;

        return s.PlayLoop(sfx, worldPos, spatial, minDist, maxDist);
    }
}
