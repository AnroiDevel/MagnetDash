using System;
using System.Runtime.InteropServices;
using UnityEngine;

[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public sealed class VkCloudStorage : MonoBehaviour, ISaveStorage
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void VK_SaveString(string key, string value);
    [DllImport("__Internal")] private static extern void VK_LoadString(string key, string goName, string methodName);
    [DllImport("__Internal")] private static extern int VK_HasBridge();
#endif

    [Header("VK")]
    [SerializeField] private string _keyPrefix = "magnet_save_";
    [SerializeField] private bool _ignoreUrlCheck = true;
    [SerializeField, Min(0.1f)] private float _loadTimeoutSeconds = 5f;

    private bool _available;

    private Action<string> _onOk;
    private Action<string> _onFail;

    private bool _waiting;
    private float _startedAt;

    public bool IsAvailable => _available;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _available = SafeHasBridge() && (_ignoreUrlCheck || IsVkEnvironmentByUrl());
#else
        _available = false;
#endif
    }

    private void Update()
    {
        if(!_waiting)
            return;

        if(Time.unscaledTime - _startedAt < _loadTimeoutSeconds)
            return;

        CompleteLoadFail("vk_load_timeout");
    }

    public void Load(string slotId, Action<string> onOk, Action<string> onFail)
    {
        if(!_available)
        {
            onFail?.Invoke("vk_unavailable");
            return;
        }

        if(_waiting)
        {
            onFail?.Invoke("vk_busy");
            return;
        }

        _onOk = onOk;
        _onFail = onFail;

        var key = MakeKey(slotId);

#if UNITY_WEBGL && !UNITY_EDITOR
        _waiting = true;
        _startedAt = Time.unscaledTime;

        try
        {
            VK_LoadString(key, gameObject.name, nameof(OnVkStorageLoaded));
        }
        catch
        {
            CompleteLoadFail("vk_load_js_exception");
        }
#else
        onFail?.Invoke("not_webgl");
#endif
    }

    public void Save(string slotId, string json)
    {
        if(!_available)
            return;

        var key = MakeKey(slotId);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            VK_SaveString(key, json ?? string.Empty);
        }
        catch(Exception e)
        {
            Debug.LogError($"[VkCloudStorage] Save exception: {e}");
        }
#endif
    }

    // JS -> Unity
    public void OnVkStorageLoaded(string json)
    {
        if(!_waiting)
            return;

        _waiting = false;

        var ok = _onOk;
        _onOk = null;

        var fail = _onFail;
        _onFail = null;

        // пуста€ строка = "нет данных" Ч это Ќ≈ ошибка
        ok?.Invoke(json ?? string.Empty);
    }

    private void CompleteLoadFail(string reason)
    {
        if(!_waiting)
            return;

        _waiting = false;

        var fail = _onFail;
        _onFail = null;

        _onOk = null;

        fail?.Invoke(reason);
    }

    private string MakeKey(string slotId)
    {
        slotId = string.IsNullOrEmpty(slotId) ? "default" : slotId.Trim();
        var prefix = string.IsNullOrEmpty(_keyPrefix) ? "magnet_save_" : _keyPrefix.Trim();
        return prefix + slotId;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private bool SafeHasBridge()
    {
        try { return VK_HasBridge() == 1; }
        catch { return false; }
    }

    private bool IsVkEnvironmentByUrl()
    {
        var url = Application.absoluteURL;
        if(string.IsNullOrEmpty(url))
            return false;

        url = url.ToLowerInvariant();
        return url.Contains("vk.com")
            || url.Contains("m.vk.com")
            || url.Contains("vk.ru")
            || url.Contains("vkplay.ru");
    }
#endif
}
