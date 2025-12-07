using System;

public interface IGameEvents
{
    event Action PortalReached;
    event Action<int> PolarityChanged;
    event Action<float> SpeedChanged;
    event Action<int, int> StarCollected;
    event Action<float> TimeChanged;      // мнбне

    void FirePortalReached();
    void FirePolarityChanged(int sign);
    void FireSpeedChanged(float speed);
    void FireStarCollected(int collected, int perLevel);
    void FireTimeChanged(float timeSeconds); // мнбне
}
