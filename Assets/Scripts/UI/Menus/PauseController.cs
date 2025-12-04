using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private CanvasGroup _panelPause;
    [SerializeField] private CanvasGroup _settingsPanel;

    [SerializeField] private float _fade = 0.15f;

    public bool IsPaused { get; private set; }
    public event Action<bool> PausedChanged;

    private IInputService _input;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.Back += OnBack; // для модалок
            _input.Pause += OnPause;      // глобальная пауза/резюм
        });
    }

    private void OnDisable()
    {
        if(_input != null)
        {
            _input.Back -= OnBack;
            _input.Pause -= OnPause;
        }
    }

    private bool SettingsOpen => _settingsPanel && _settingsPanel.alpha > 0f;


    private void OnPause()
    {
        OnPausePressed(); // уже содержит логику переключения паузы через LevelManager.State
    }

    /// <summary>
    /// Вызывается кнопкой/вводом «Пауза».
    /// </summary>
    public void OnPausePressed()
    {
        if(!ServiceLocator.TryGet<LevelManager>(out var lm))
            return;

        switch(lm.State)
        {
            case GameState.Playing:
                // Входим в паузу
                lm.Pause();   // меняем состояние игры
                Pause();      // приводим UI и ввод в соответствие
                break;

            case GameState.Paused:
                // Выходим из паузы
                lm.Resume();
                Resume();
                break;

            default:
                // В других состояниях пауза не работает
                break;
        }
    }

    private void Pause()
    {
        if(IsPaused)
            return;

        IsPaused = true;
        Time.timeScale = 0f;
        _input?.EnableGameplay(false); // отключаем геймплейный ввод
        Show(_panelPause, true);
        PausedChanged?.Invoke(true);
    }

    private void Resume()
    {
        // Сначала закрываем настройки, если они открыты
        if(SettingsOpen)
        {
            CloseSettings();
            return;
        }

        if(!IsPaused)
            return;

        IsPaused = false;
        Show(_panelPause, false);
        Time.timeScale = 1f;
        _input?.EnableGameplay(true);
        PausedChanged?.Invoke(false);
    }

    public void OpenSettings()
    {
        if(_settingsPanel)
        {
            StartCoroutine(Fade(_settingsPanel, true, _fade));
            _input?.PushModal();
        }
    }

    public void CloseSettings()
    {
        if(_settingsPanel && _settingsPanel.alpha > 0f)
        {
            StartCoroutine(Fade(_settingsPanel, false, _fade));
            _input?.PopModal();
        }
    }

    private void OnBack()
    {
        if(SettingsOpen)
        {
            CloseSettings();
            return;
        }

        OnPausePressed();
    }

    private void Show(CanvasGroup cg, bool show)
    {
        if(!cg)
            return;

        StopAllCoroutines();
        StartCoroutine(Fade(cg, show, _fade));
    }

    private IEnumerator Fade(CanvasGroup cg, bool show, float t)
    {
        cg.blocksRaycasts = true;
        cg.interactable = show;

        float a0 = cg.alpha;
        float a1 = show ? 1f : 0f;
        float tt = 0f;

        while(tt < t)
        {
            tt += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a0, a1, tt / t);
            yield return null;
        }

        cg.alpha = a1;

        if(!show)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }

    public void OnRestart()
    {
        // Сначала выходим из паузы (UI + тайм)
        Resume();

        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.Reload();
        }
        else
        {
            Debug.LogError("[PauseController] ILevelFlow service not found. " +
                           "LevelManager / Systems scene misconfigured.");
        }
    }

    public void OnExitToMenu()
    {
        // Тоже выходим из паузы (на всякий случай)
        Resume();

        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.LoadMenu();
        }
        else
        {
            Debug.LogError("[PauseController] ILevelFlow service not found. " +
                           "LevelManager / Systems scene misconfigured.");
        }
    }
}
