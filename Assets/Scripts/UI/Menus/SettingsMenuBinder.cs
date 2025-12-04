using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsMenuBinder : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider _sldMaster;
    [SerializeField] private Slider _sldSfx;
    [SerializeField] private Slider _sldUi;
    [SerializeField] private Slider _sldMusic;
    [SerializeField] private Toggle _tgMute;

    [SerializeField] private TMP_Text _tMaster;
    [SerializeField] private TMP_Text _tSfx;
    [SerializeField] private TMP_Text _tUi;
    [SerializeField] private TMP_Text _tMusic;

    [SerializeField] private Toggle _tgHints;

    [SerializeField] private Button _resetProgressBtn;

    private ISettingsService _settings;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IAudioService>(audio =>
        {
            InitValues(audio);
        });

        ServiceLocator.WhenAvailable<ISettingsService>(s =>
        {
            _settings = s;
            _tgHints.SetIsOnWithoutNotify(_settings.HintsEnabled);
            _tgHints.onValueChanged.AddListener(on => _settings.HintsEnabled = on);
        });

        ServiceLocator.WhenAvailable<IProgressService>(p => 
        {
            _resetProgressBtn.onClick.AddListener(() => p.ResetAll());
        });

    }

    private void InitValues(IAudioService audio)
    {
        SetSlider(_sldMaster, _tMaster, audio.MasterVolume01, v => audio.MasterVolume01 = v);
        SetSlider(_sldMusic, _tMusic, audio.MusicVolume01, v => audio.MusicVolume01 = v);
        SetSlider(_sldSfx, _tSfx, audio.SfxVolume01, v => audio.SfxVolume01 = v);
        SetSlider(_sldUi, _tUi, audio.UIVolume01, v => audio.UIVolume01 = v);

        _tgMute.isOn = !audio.Muted;
        _tgMute.onValueChanged.AddListener(v => audio.Muted = !v);

    }

    private void SetSlider(Slider slider, TMP_Text label, float value, System.Action<float> onChange)
    {
        slider.value = value;
        SetValueText(label, value);
        slider.onValueChanged.AddListener(v =>
        {
            onChange(v);
            SetValueText(label, v);
        });
    }

    private void SetValueText(TMP_Text text, float v)
    {
        int p = Mathf.RoundToInt(v * 100f);
        text.SetText("{0}%", p);
    }

    private void OnDisable()
    {
        _tgHints.onValueChanged.RemoveAllListeners();

        _resetProgressBtn.onClick.RemoveAllListeners();

        _sldMaster.onValueChanged.RemoveAllListeners();
        _sldMusic.onValueChanged.RemoveAllListeners();
        _sldSfx.onValueChanged.RemoveAllListeners();
        _sldUi.onValueChanged.RemoveAllListeners();
        _tgMute.onValueChanged.RemoveAllListeners();
    }

}
