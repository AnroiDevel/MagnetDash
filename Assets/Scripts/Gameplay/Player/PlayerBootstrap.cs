using UnityEngine;

public sealed class PlayerBootstrap : MonoBehaviour
{
    private IPlayerRegistry _registry;

    private void Awake()
    {
        ServiceLocator.WhenAvailable<IPlayerRegistry>(r =>
        {
            _registry = r;
            _registry.Register(transform);
        });
    }
}
