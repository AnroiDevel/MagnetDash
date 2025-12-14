using System;
using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VkAdService : MonoBehaviour, IAdService
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void VK_ShowRewardedAd(string goName, string onSuccess, string onFail);
    [DllImport("__Internal")] private static extern int VK_HasBridge();                 // 1/0
    [DllImport("__Internal")] private static extern void VK_Log(string msg);            // console.log
    [DllImport("__Internal")] private static extern string VK_GetUrl();                 // absoluteURL as seen by JS
    [DllImport("__Internal")] private static extern void VK_PreloadRewardedAd(string goName, string onDone); // "1"/"0"
#endif

    [Header("Debug")]
    [SerializeField] private bool _forceAvailableInWebGL = true;   // для дебага: не зависеть от URL
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
        // 1) Проверяем наличие vkBridge (через JS)
        bool hasBridge = SafeHasBridge();

        // 2) Дебажный availability
        _available = _forceAvailableInWebGL ? hasBridge : (hasBridge && IsVkEnvironmentByUrl());

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
#if UNITY_WEBGL && !UNITY_EDITOR
        if(!_waiting)
            return;

        float elapsed = Time.unscaledTime - _startedAt;
        if(elapsed >= _callTimeoutSeconds)
        {
            _waiting = false;
            if(_verbose) Debug.LogWarning($"[VkAdService] Rewarded timeout after {elapsed:0.0}s");
            SafeJsLog($"[VkAdService] Rewarded timeout after {elapsed:0.0}s");
            FailInternal("timeout");
        }
#endif
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
        _rewardedReady = true;
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
            _waiting = false;
            Debug.LogError($"[VkAdService] Exception calling JS: {e}");
            FailInternal("js_call_exception");
        }
#else
        if(_verbose)
            Debug.Log("[VkAdService] Editor/Non-WebGL stub -> simulate success");
        onSuccess?.Invoke();
#endif
    }

    // JS -> Unity callbacks
    public void OnJsSuccess(string payload)
    {
        if(_verbose)
            Debug.Log($"[VkAdService] OnJsSuccess payload='{payload}'");
#if UNITY_WEBGL && !UNITY_EDITOR
        SafeJsLog($"[VkAdService] OnJsSuccess payload='{payload}'");
        _waiting = false;
#endif
        _success?.Invoke();
        _success = null;
        _fail = null;
    }

    public void OnJsFail(string reason)
    {
        if(_verbose)
            Debug.LogWarning($"[VkAdService] OnJsFail reason='{reason}'");
#if UNITY_WEBGL && !UNITY_EDITOR
        SafeJsLog($"[VkAdService] OnJsFail reason='{reason}'");
        _waiting = false;
#endif
        FailInternal(reason);
    }

    private void FailInternal(string reason)
    {
        _fail?.Invoke();
        _success = null;
        _fail = null;
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
        try
        {
            if(_verbose)
                VK_Log(msg);
        }
        catch { }
    }

    private string SafeGetUrl()
    {
        try { return VK_GetUrl(); }
        catch { return string.Empty; }
    }
#endif
}
