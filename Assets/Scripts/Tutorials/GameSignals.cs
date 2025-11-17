using System;

public static class GameSignals
{
    public static event Action PolaritySwitched;  // вызови при смене пол€рности
    public static event Action PortalReached;     // вызови в портале при победе

    public static void FirePolaritySwitched() => PolaritySwitched?.Invoke();
    public static void FirePortalReached() => PortalReached?.Invoke();
}
