using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameEventsService : MonoBehaviour, IGameEvents
{
    public event Action PolaritySwitched;
    public event Action PortalReached;

    public void FirePolaritySwitched() => PolaritySwitched?.Invoke();
    public void FirePortalReached() => PortalReached?.Invoke();

    private void Awake()
    {
        ServiceLocator.Register<IGameEvents>(this);
    }
}
