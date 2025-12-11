using UnityEngine;
using System;
using System.Runtime.InteropServices;

public sealed class VkAdService : MonoBehaviour, IAdService
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VK_ShowRewardedAd(string goName, string onSuccess, string onFail);
#endif

    private bool _available;
    private Action _success;
    private Action _fail;

    public bool IsAvailable => _available;

    private void Awake()
    {
        _available = IsVkEnvironment();

        if(_available)
        {
            ServiceLocator.Register<IAdService>(this);
            Debug.Log("[VkAdService] Registered in ServiceLocator");
        }
        else
        {
            Debug.Log("[VkAdService] Not VK environment → service not registered");
        }
    }

    private void OnDestroy()
    {
        if(_available)
        {
            ServiceLocator.Register<IAdService>(null);
            Debug.Log("[VkAdService] Unregistered");
        }
    }

    public void ShowRewarded(Action onSuccess, Action onFail = null)
    {
        if(!_available)
        {
            Debug.LogWarning("[VkAdService] ShowRewarded() called, but service unavailable");
            onFail?.Invoke();
            return;
        }

        _success = onSuccess;
        _fail = onFail;

#if UNITY_WEBGL && !UNITY_EDITOR
        VK_ShowRewardedAd(gameObject.name, nameof(OnJsSuccess), nameof(OnJsFail));
#else
        Debug.Log("[VkAdService] Editor stub → simulate success");
        OnJsSuccess("");
#endif
    }

    // JS → Unity callbacks
    public void OnJsSuccess(string _)
    {
        _success?.Invoke();
        _success = null;
        _fail = null;
    }

    public void OnJsFail(string _)
    {
        _fail?.Invoke();
        _success = null;
        _fail = null;
    }

    private bool IsVkEnvironment()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string url = Application.absoluteURL.ToLowerInvariant();
        if (string.IsNullOrEmpty(url))
            return false;

        return url.Contains("vk.com") || url.Contains("vkplay.ru");
#else
        return false;
#endif
    }
}
