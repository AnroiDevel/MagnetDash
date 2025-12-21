using UnityEngine;

public static class SaveStorageFactory
{
    public static ISaveStorage Create(MonoBehaviour host, string slotId, string localFilePrefix, string vkKeyPrefix)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // В WebGL: пробуем VK storage (если недоступно — можно упасть на local)
        var vk = host.GetComponent<VkCloudStorage>();
        if(vk == null)
            vk = host.gameObject.AddComponent<VkCloudStorage>();

        // прокинем префикс ключей (если нужно)
        // (в инспекторе можно тоже настроить, но так надёжнее)
        var so = vk; // просто чтобы не плодить лишнее

        // Если VK доступен — возвращаем его
        if(so.IsAvailable)
            return so;

        // Иначе local (WebGL persistentDataPath тоже работает, но это уже НЕ облако)
        return new LocalFileStorage(Application.persistentDataPath, localFilePrefix);
#else
        return new LocalFileStorage(Application.persistentDataPath, localFilePrefix);
#endif
    }
}
