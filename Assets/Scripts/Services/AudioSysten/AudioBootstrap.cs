using UnityEngine;

/// <summary>
/// Регистрирует реализацию аудиосервиса один раз на старте.
/// Помести этот компонент на префаб AudioSystem (где висит AudioManager),
/// сцену Bootstrap сделай первой в Build Settings.
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioBootstrap : MonoBehaviour
{
    [SerializeField] private AudioManager _impl;

    private void Awake()
    {
        //if(_impl == null)
        //{
        //    // Разрешаем висеть на том же объекте, что и AudioManager
        //    _impl = GetComponent<AudioManager>();
        //}

        //if(_impl == null)
        //{
        //    // Страховка: создадим AudioManager, если кто-то удалил ссылку.
        //    var go = new GameObject("AudioManager");
        //    _impl = go.AddComponent<AudioManager>();
        //}

        //DontDestroyOnLoad(_impl.gameObject);

        // Зарегистрировать реализацию в фасаде Audio
        Audio.Initialize(_impl);
    }
}
