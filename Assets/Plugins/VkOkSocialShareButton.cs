using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

[DisallowMultipleComponent]
public sealed class VkOkSocialShareButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _shareRoot; // что скрывать/показывать (например, саму кнопку)
    [SerializeField] private Button _button;        // сама кнопка share

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void VK_RequestPlatform(string goName);

    [DllImport("__Internal")]
    private static extern void VK_SocialAction();
#else
    private static void VK_RequestPlatform(string goName)
    {
        Debug.Log("[VkOkSocialShareButton] VK_RequestPlatform stub (editor)");
    }

    private static void VK_SocialAction()
    {
        Debug.Log("[VkOkSocialShareButton] VK_SocialAction stub (editor)");
    }
#endif

    private string _platform = "unknown";

    private void Awake()
    {
        if(_button != null)
            _button.onClick.AddListener(OnClick);
    }

    private void Start()
    {
        if(_shareRoot == null)
            _shareRoot = gameObject;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Передаём имя GameObject, на котором висит этот скрипт
        VK_RequestPlatform(gameObject.name);
#else
        // В редакторе считаем, что это VK
        OnPlatform("vk");
#endif
    }

    private void OnDestroy()
    {
        if(_button != null)
            _button.onClick.RemoveListener(OnClick);
    }

    // Вызывается из JS (SendMessage(goName, "OnPlatform", platform))
    public void OnPlatform(string platform)
    {
        _platform = platform;
        bool isOK = platform == "ok";

        if(_shareRoot != null)
            _shareRoot.SetActive(!isOK);

        Debug.Log($"[VkOkSocialShareButton] platform={platform}, share active={!isOK}");
    }

    private void OnClick()
    {
        // В ОК кнопка будет скрыта, но на всякий случай проверим.
        if(_platform == "ok")
        {
            Debug.Log("[VkOkSocialShareButton] click ignored on OK");
            return;
        }

        VK_SocialAction();
    }
}
