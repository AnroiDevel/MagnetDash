using UnityEngine;

public static class Audio
{
    private static IAudioService _service;
    private static bool _subscribed;

    // Важно: сброс статики при старте PlayMode / домена
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _service = null;
        _subscribed = false;
    }

    // Подписываемся после загрузки сборок
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Bootstrap()
    {
        SubscribeOnce();
        TryResolve();
    }

    private static void SubscribeOnce()
    {
        if(_subscribed)
            return;

        _subscribed = true;

        // Когда сервис появится — подхватим ссылку
        ServiceLocator.WhenAvailable<IAudioService>(OnAudioAvailable);
    }

    private static void OnAudioAvailable(IAudioService service)
    {
        _service = service;
    }

    private static void TryResolve()
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
            // Страховка: если сервис пересоздали/пере-регистрировали — подхватим заново
            if(_service == null)
                TryResolve();

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
