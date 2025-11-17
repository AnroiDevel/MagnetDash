using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SettingsService : MonoBehaviour, ISettingsService
{
    private const string KHints = "settings.hints";
    public bool HintsEnabled
    {
        get => _hintsEnabled;
        set
        {
            if(_hintsEnabled == value)
                return;
            _hintsEnabled = value;
            PlayerPrefs.SetInt(KHints, value ? 1 : 0);
            PlayerPrefs.Save();
            HintsChanged?.Invoke(_hintsEnabled);
        }
    }

    public event Action<bool> HintsChanged;
    private bool _hintsEnabled = true;

    private void Awake()
    {
        _hintsEnabled = PlayerPrefs.GetInt(KHints, 1) == 1;
        ServiceLocator.Register<ISettingsService>(this);
    }
}
