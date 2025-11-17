using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsMenuBinder : MonoBehaviour
{
    [Header("Service (assign explicitly)")]
    private AudioManager _audio;

    private ISettingsService _settings;

    [Header("UI")]
    [SerializeField] private Slider _slMaster;
    [SerializeField] private Slider _slSfx;
    [SerializeField] private Slider _slUi;
    [SerializeField] private Slider _slMusic;
    [SerializeField] private Toggle _tgMute;

    [SerializeField] private TMP_Text _tMaster;
    [SerializeField] private TMP_Text _tSfx;
    [SerializeField] private TMP_Text _tUi;
    [SerializeField] private TMP_Text _tMusic;

    [SerializeField] private Toggle _tgHints;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<AudioManager>(InitValues);

        ServiceLocator.WhenAvailable<ISettingsService>(s =>
        {
            _settings = s;
            _tgHints.SetIsOnWithoutNotify(_settings.HintsEnabled);
            _tgHints.onValueChanged.AddListener(on => _settings.HintsEnabled = on);
        });

        // Подписки
        if(_slMaster)
            _slMaster.onValueChanged.AddListener(OnMasterChanged);
        if(_slSfx)
            _slSfx.onValueChanged.AddListener(OnSfxChanged);
        if(_slUi)
            _slUi.onValueChanged.AddListener(OnUiChanged);
        if(_slMusic)
            _slMusic.onValueChanged.AddListener(OnMusicChanged);
        if(_tgMute)
            _tgMute.onValueChanged.AddListener(OnMuteChanged);
    }

    private void InitValues(AudioManager audio)
    {
        _audio = audio;

        if(_slMaster)
        {
            var v = _audio.GetMasterVolume01();
            _slMaster.value = v;

            int p = Mathf.RoundToInt(v * 100f);
            _tMaster.SetText("{0}%", p);
        }

        if(_slSfx)
        {
            var v = _audio.GetSfxVolume01();
            _slSfx.value = v;

            int p = Mathf.RoundToInt(v * 100f);
            _tSfx.SetText("{0}%", p);
        }

        if(_slUi)
        {
            var v = _audio.GetUiVolume01();
            _slUi.value = v;

            int p = Mathf.RoundToInt(v * 100f);
            _tUi.SetText("{0}%", p);
        }

        if(_slMusic)
        {
            var v = _audio.GetMusicVolume01();
            _slMusic.value = v;

            int p = Mathf.RoundToInt(v * 100f);
            _tMusic.SetText("{0}%", p);
        }

        if(_tgMute)
            _tgMute.isOn = _audio.GetMuted();
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<AudioManager>(InitValues);

        _tgHints?.onValueChanged.RemoveAllListeners();

        // Снятие подписок (чисто)
        if(_slMaster)
            _slMaster.onValueChanged.RemoveListener(OnMasterChanged);
        if(_slSfx)
            _slSfx.onValueChanged.RemoveListener(OnSfxChanged);
        if(_slUi)
            _slUi.onValueChanged.RemoveListener(OnUiChanged);
        if(_slMusic)
            _slMusic.onValueChanged.RemoveListener(OnMusicChanged);
        if(_tgMute)
            _tgMute.onValueChanged.RemoveListener(OnMuteChanged);
    }

    // Handlers
    private void OnMasterChanged(float v)
    {
        _audio.SetMasterVolume01(v);

        int p = Mathf.RoundToInt(v * 100f);
        _tMaster.SetText("{0}%", p); 
    }

    private void OnSfxChanged(float v)
    {
        _audio.SetSfxVolume01(v);

        int p = Mathf.RoundToInt(v * 100f);
        _tSfx.SetText("{0}%", p);
    }

    private void OnUiChanged(float v)
    {
        _audio.SetUiVolume01(v);

        int p = Mathf.RoundToInt(v * 100f);
        _tUi.SetText("{0}%", p);
    }

    private void OnMusicChanged(float v)
    {
        _audio.SetMusicVolume01(v);

        int p = Mathf.RoundToInt(v * 100f);
        _tMusic.SetText("{0}%", p);
    }

    private void OnMuteChanged(bool m) => _audio.SetMuted(m);
}
