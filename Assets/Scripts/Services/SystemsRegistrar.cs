// SystemsRegistrar.cs (положи в сцену Systems)
using UnityEngine;

public sealed class SystemsRegistrar : MonoBehaviour
{
    [SerializeField] private AudioManager _audio;

    private void Awake()
    {
        if(_audio == null)
            _audio = GetComponentInChildren<AudioManager>(true);
        ServiceLocator.Register(_audio);
        // при желании: ServiceLocator.Register<IMusicService>(_audio);
    }
}
