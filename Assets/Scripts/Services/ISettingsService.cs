using System;

public interface ISettingsService
{
    bool HintsEnabled { get; set; }
    event Action<bool> HintsChanged;
}
