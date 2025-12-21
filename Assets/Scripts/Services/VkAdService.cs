using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class VkAdService : MonoBehaviour, IAdService
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void VK_ShowRewardedAd(string goName, string onSuccess, string onFail);
    [DllImport("__Internal")] private static extern int VK_HasBridge();
    [DllImport("__Internal")] private static extern void VK_Log(string msg);
    [DllImport("__Internal")] private static extern string VK_GetUrl();
    [DllImport("__Internal")] private static extern void VK_PreloadRewardedAd(string goName, string onDone); // "1"/"0" or JSON
#endif

    [Header("Availability")]
    [SerializeField] private bool _ignoreUrlCheck = true;
    [SerializeField] private bool _verbose = true;

    [Header("Timeouts")]
    [SerializeField, Min(0.1f)] private float _callTimeoutSeconds = 120f;

    [Header("Auto preload")]
    [SerializeField] private string _systemsSceneName = "Systems";
    [SerializeField] private int _menuSceneIndex = 0;
    [SerializeField] private int _firstLevelSceneIndex = 1;
    [SerializeField, Min(0f)] private float _preloadCooldownSeconds = 2.0f;

    private bool _available;
    private bool _registered;

    private Action _success;
    private Action _fail;

    private bool _waiting;
    private float _startedAt;

    private bool _rewardedReady;
    private float _nextAllowedPreloadTime;

    // Pending show (если пользователь нажал до готовности)
    private bool _pendingShow;
    private Action _pendingSuccess;
    private Action _pendingFail;

    public bool IsAvailable => _available;
    public bool IsRewardedReady => _rewardedReady;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        bool hasBridge = SafeHasBridge();
        _available = hasBridge && (_ignoreUrlCheck || IsVkEnvironmentByUrl());

        if(_verbose)
        {
            Debug.Log($"[VkAdService] Awake url(Unity)='{Application.absoluteURL}' available={_available} hasBridge={hasBridge}");
            SafeJsLog($"[VkAdService] Awake url(JS)='{SafeGetUrl()}' available={_available} hasBridge={hasBridge}");
        }

        if(_available)
        {
            ServiceLocator.Register<IAdService>(this);
            _registered = true;
        }
        else
        {
            _rewardedReady = false;
        }
#else
        _available = false;
        _rewardedReady = false;
#endif
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        TryAutoPreload("enable");
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if(_registered)
        {
            ServiceLocator.Unregister<IAdService>(this);
            _registered = false;
        }
    }

    private void Update()
    {
        if(!_waiting)
            return;

        float elapsed = Time.unscaledTime - _startedAt;
        if(elapsed < _callTimeoutSeconds)
            return;

        Complete(success: false, reason: $"timeout:{elapsed:0.0}s");
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if(IsGameplayScene(newScene))
            TryAutoPreload("scene_changed_to_gameplay");
    }

    public void PreloadRewarded()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(!_available)
            return;

        if(_rewardedReady)
            return;

        if(_waiting)
            return;

        float now = Time.unscaledTime;
        if(now < _nextAllowedPreloadTime)
            return;

        _nextAllowedPreloadTime = now + _preloadCooldownSeconds;

        try
        {
            if(_verbose) Debug.Log("[VkAdService] PreloadRewarded...");
            VK_PreloadRewardedAd(gameObject.name, nameof(OnJsRewardedReady));
        }
        catch
        {
            _rewardedReady = false;
        }
#else
        _rewardedReady = false;
#endif
    }

    private void TryAutoPreload(string reason)
    {
        if(!_available)
            return;

        if(!IsGameplayScene(SceneManager.GetActiveScene()))
            return;

        if(_verbose)
            Debug.Log($"[VkAdService] AutoPreload reason='{reason}' ready={_rewardedReady} waiting={_waiting} pending={_pendingShow}");

        PreloadRewarded();
    }

    public void ShowRewarded(Action onSuccess, Action onFail = null)
    {
        if(!_available)
        {
            onFail?.Invoke();
            return;
        }

        if(_waiting)
        {
            onFail?.Invoke();
            return;
        }

        // Если не готово — ставим pending и прелоадим.
        if(!_rewardedReady)
        {
            _pendingShow = true;
            _pendingSuccess = onSuccess;
            _pendingFail = onFail;

            if(_verbose)
                Debug.Log("[VkAdService] ShowRewarded: not ready -> Preload + pending");

            PreloadRewarded();
            return;
        }

        InternalShowRewarded(onSuccess, onFail);
    }

    private void InternalShowRewarded(Action onSuccess, Action onFail)
    {
        _success = onSuccess;
        _fail = onFail;

        // Готовность “расходуем” только в момент реального show
        _rewardedReady = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        _waiting = true;
        _startedAt = Time.unscaledTime;

        try
        {
            if(_verbose) Debug.Log("[VkAdService] Calling JS VK_ShowRewardedAd...");
            SafeJsLog("[VkAdService] Calling JS VK_ShowRewardedAd...");
            VK_ShowRewardedAd(gameObject.name, nameof(OnJsSuccess), nameof(OnJsFail));
        }
        catch(Exception e)
        {
            if(_verbose) Debug.LogError($"[VkAdService] Exception calling JS: {e}");
            Complete(success: false, reason: "js_call_exception");
        }
#else
        onSuccess?.Invoke();
#endif
    }

    private void ClearPending()
    {
        _pendingShow = false;
        _pendingSuccess = null;
        _pendingFail = null;
    }

    // -------------------------
    // JS -> Unity callbacks
    // ВАЖНО: в Web версии часто прилетает вызов БЕЗ аргумента.
    // Поэтому держим overload без параметров.
    // -------------------------

    // Rewarded preload done
    public void OnJsRewardedReady() => OnJsRewardedReady("1");

    public void OnJsRewardedReady(string payload)
    {
        _rewardedReady = ParseBool(payload);

        if(_verbose)
            Debug.Log($"[VkAdService] PreloadRewarded result={_rewardedReady} payload='{payload}' pending={_pendingShow}");

        if(!_pendingShow)
            return;

        if(_rewardedReady)
        {
            var ok = _pendingSuccess;
            var fail = _pendingFail;
            ClearPending();
            InternalShowRewarded(ok, fail);
        }
        else
        {
            var fail = _pendingFail;
            ClearPending();
            fail?.Invoke();
        }
    }

    // Ad success
    public void OnJsSuccess() => OnJsSuccess("ok");

    public void OnJsSuccess(string payload)
    {
        if(_verbose)
            Debug.Log($"[VkAdService] OnJsSuccess payload='{payload}'");

        Complete(success: true, reason: payload);
    }

    // Ad fail
    public void OnJsFail() => OnJsFail("fail");

    public void OnJsFail(string reason)
    {
        if(_verbose)
            Debug.LogWarning($"[VkAdService] OnJsFail reason='{reason}'");

        Complete(success: false, reason: reason);
    }

    private void Complete(bool success, string reason)
    {
        if(_waiting)
            _waiting = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        SafeJsLog($"[VkAdService] Complete success={success} reason='{reason}'");
#endif

        var onSuccess = _success;
        var onFail = _fail;

        _success = null;
        _fail = null;

        if(success)
            onSuccess?.Invoke();
        else
            onFail?.Invoke();

        TryAutoPreload("after_complete");
    }

    private bool IsGameplayScene(Scene scene)
    {
        if(!scene.IsValid())
            return false;

        if(scene.name == _systemsSceneName)
            return false;

        if(scene.buildIndex == _menuSceneIndex)
            return false;

        return scene.buildIndex >= _firstLevelSceneIndex;
    }

    private static bool ParseBool(string payload)
    {
        if(string.IsNullOrEmpty(payload))
            return false;

        payload = payload.Trim();

        // Частые варианты: "1"/"0", "true"/"false"
        if(payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        if(payload == "0" || payload.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        // Иногда прилетает JSON вида {"ready":true} или {"result":1}
        // Без JSON парсера — просто эвристика по подстрокам.
        string lower = payload.ToLowerInvariant();
        if(lower.Contains("true") || lower.Contains(":1") || lower.Contains("\"ready\":1") || lower.Contains("\"ready\":true"))
            return true;

        return false;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private bool IsVkEnvironmentByUrl()
    {
        var url = Application.absoluteURL;
        if(string.IsNullOrEmpty(url))
            return false;

        url = url.ToLowerInvariant();
        return url.Contains("vk.com") || url.Contains("vkplay.ru") || url.Contains("vk.ru");
    }

    private bool SafeHasBridge()
    {
        try { return VK_HasBridge() == 1; }
        catch { return false; }
    }

    private void SafeJsLog(string msg)
    {
        if(!_verbose)
            return;

        try { VK_Log(msg); }
        catch { }
    }

    private string SafeGetUrl()
    {
        try { return VK_GetUrl(); }
        catch { return string.Empty; }
    }
#endif
}
