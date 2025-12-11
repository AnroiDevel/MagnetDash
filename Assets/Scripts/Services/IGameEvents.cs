using System;
using UnityEngine;

public interface IGameEvents
{
    event Action PortalReached;
    event Action<int> PolarityChanged;
    event Action<float> SpeedChanged;

    /// <summary>
    /// StarCollected(collectedCount, worldPos)
    /// collectedCount - текущее общее количество собранных звёзд в уровне.
    /// worldPos       - позиция последней собранной звезды.
    /// </summary>
    event Action<int, Vector3> StarCollected;

    event Action<float> TimeChanged;  

    void FirePortalReached();
    void FirePolarityChanged(int sign);
    void FireSpeedChanged(float speed);
    void FireStarCollected(int collected, Vector3 pos);
    void FireTimeChanged(float timeSeconds); 
}
