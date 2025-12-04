using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameEventsService : MonoBehaviour, IGameEvents
{
    public event Action PortalReached;

    public event Action<int> PolarityChanged;
    public event Action<float> SpeedChanged;


    private void Awake()
    {
        ServiceLocator.Register<IGameEvents>(this);
    }

    public void FirePolarityChanged(int sign)
    {
        PolarityChanged?.Invoke(sign);
    }

    public void FireSpeedChanged(float speed)
    {
        SpeedChanged?.Invoke(speed);
    }

    public void FirePortalReached() => PortalReached?.Invoke();
}
