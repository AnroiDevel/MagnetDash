using System;

public interface IGameEvents
{
    event Action PolaritySwitched;
    event Action PortalReached;

    void FirePolaritySwitched();
    void FirePortalReached();
}