using System;

public interface IGameEvents
{
    event Action PortalReached;

    event Action<int> PolarityChanged;
    event Action<float> SpeedChanged;

    void FirePortalReached();

    void FirePolarityChanged(int sign);
    void FireSpeedChanged(float speed);
}