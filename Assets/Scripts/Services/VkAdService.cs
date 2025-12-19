using System;
using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VkAdService : MonoBehaviour, IAdService
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void VK_ShowRewardedAd(string goName, string onSuccess, string onFail);
    [DllImport("__Internal")] private static extern int VK_HasBridge();
    [DllImport("__Internal")] private static extern void VK_Log(string msg);
    [DllImport("__Internal")] private static extern string VK_GetUrl();
    [DllImport("__Internal")] private static extern void VK_PreloadRewardedAd(string goName, string onDone); // "1"/"0"
#endif

    [Header("Debug")]
    [SerializeField] private bool _ignoreUrlCheck = true; // раньше _forceAvailableInWebGL, фактически это "не проверять URL"
    [SerializeField] private bool _verbose = true;
    [SerializeField] private float _callTimeoutSeconds = 10f;

    private bool _available;
    private bool _registered;

    private Action _success;
    private Action _fail;

    private bool _waiting;
    private float _startedAt;

    private bool _rewardedReady;
    public bool IsRewardedReady => _rewardedReady;
    public bool IsAvailable => _available;

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
            if(_verbose) Debug.Log("[VkAdService] Registered IAdService");
        }
        else
        {
            if(_verbose) Debug.LogWarning("[VkAdService] Not available -> not registered");
        }
#else
        _available = false;
        _rewardedReady = false; // важно: недоступно => не "готово"
        if(_verbose)
            Debug.Log("[VkAdService] Not WebGL build -> unavailable");
#endif
    }

    private void OnDestroy()
    {
        if(_registered)
        {
            ServiceLocator.Unregister<IAdService>(this);
            _registered = false;
            if(_verbose)
                Debug.Log("[VkAdService] Unregistered IAdService");
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

    public void PreloadRewarded()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(!_available)
            return;

        try
        {
            VK_PreloadRewardedAd(gameObject.name, nameof(OnJsRewardedReady));
        }
        catch
        {
            _rewardedReady = false;
        }
#else
        // В не-WebGL среде ничего не прелоадим.
        _rewardedReady = false;
#endif
    }

    // JS -> Unity
    public void OnJsRewardedReady(string payload)
    {
        _rewardedReady = payload == "1";
        if(_verbose)
            Debug.Log($"[VkAdService] PreloadRewarded result={_rewardedReady}");
    }

    public void ShowRewarded(Action onSuccess, Action onFail = null)
    {
        if(_verbose)
            Debug.Log($"[VkAdService] ShowRewarded called. available={_available} waiting={_waiting}");

#if UNITY_WEBGL && !UNITY_EDITOR
        SafeJsLog($"[VkAdService] ShowRewarded called. available={_available} waiting={_waiting}");
#endif

        if(!_available)
        {
            if(_verbose)
                Debug.LogWarning("[VkAdService] ShowRewarded: service unavailable");
            onFail?.Invoke();
            return;
        }

        if(_waiting)
        {
            if(_verbose)
                Debug.LogWarning("[VkAdService] ShowRewarded: already waiting (ignored)");
            onFail?.Invoke();
            return;
        }

        _success = onSuccess;
        _fail = onFail;

        // Показываем — считаем, что "готовность" потрачена
        _rewardedReady = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        _waiting = true;
        _startedAt = Time.unscaledTime;

        if(_verbose) Debug.Log("[VkAdService] Calling JS VK_ShowRewardedAd...");
        SafeJsLog("[VkAdService] Calling JS VK_ShowRewardedAd...");

        try
        {
            VK_ShowRewardedAd(gameObject.name, nameof(OnJsSuccess), nameof(OnJsFail));
        }
        catch(Exception e)
        {
            if(_verbose) Debug.LogError($"[VkAdService] Exception calling JS: {e}");
            Complete(success: false, reason: "js_call_exception");
        }
#else
        if(_verbose)
            Debug.Log("[VkAdService] Non-WebGL -> simulate success");
        onSuccess?.Invoke();
#endif
    }

    // JS -> Unity callbacks
    public void OnJsSuccess(string payload)
    {
        if(_verbose)
            Debug.Log($"[VkAdService] OnJsSuccess payload='{payload}'");
        Complete(success: true, reason: payload);
    }

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
