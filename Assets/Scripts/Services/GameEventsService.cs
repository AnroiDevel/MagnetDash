using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameEventsService : MonoBehaviour, IGameEvents
{
    public event Action PortalReached;
    public event Action<int> PolarityChanged;
    public event Action<float> SpeedChanged;
    public event Action<int, Vector3> StarCollected;
    public event Action<float> TimeChanged;   // мнбне

    private void Awake()
    {
        ServiceLocator.Register<IGameEvents>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IGameEvents>(this);
    }

    public void FirePortalReached() => PortalReached?.Invoke();
    public void FirePolarityChanged(int sign) => PolarityChanged?.Invoke(sign);
    public void FireSpeedChanged(float speed) => SpeedChanged?.Invoke(speed);
    public void FireStarCollected(int c, Vector3 pos) => StarCollected?.Invoke(c, pos);
    public void FireTimeChanged(float t) => TimeChanged?.Invoke(t); // мнбне

}
