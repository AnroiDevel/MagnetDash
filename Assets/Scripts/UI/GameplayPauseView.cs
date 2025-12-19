using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayPauseView : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private CanvasGroup _pausePanel;
    [SerializeField] private CanvasGroup _settingsPanel;

    [Header("Fade")]
    [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.15f;

    private PauseController _pause;
    private Coroutine _fadeRoutine;

    private bool SettingsOpen => _settingsPanel != null && _settingsPanel.alpha > 0.001f;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<PauseController>(pc =>
        {
            if(_pause != null)
                Unbind(_pause);

            _pause = pc;
            Bind(_pause);

            // привести UI в соответствие текущему состоянию
            ApplyPausedInstant(_pause.IsPaused);
            CloseSettingsInstant();
        });
    }

    private void OnDisable()
    {
        if(_pause != null)
            Unbind(_pause);
    }

    private void Bind(PauseController pc)
    {
        pc.PausedChanged += OnPausedChanged;
        pc.TryConsumeBack += TryConsumeBack;
    }

    private void Unbind(PauseController pc)
    {
        pc.PausedChanged -= OnPausedChanged;
        pc.TryConsumeBack -= TryConsumeBack;
    }

    private void OnPausedChanged(bool paused)
    {
        // при выходе из паузы — закрываем настройки
        if(!paused)
            CloseSettingsInstant();

        Fade(_pausePanel, paused);
    }

    private bool TryConsumeBack()
    {
        if(!SettingsOpen)
            return false;

        CloseSettings();
        return true;
    }

    // --- Public UI buttons (hook from inspector) ---

    public void OpenSettings()
    {
        if(_settingsPanel == null)
            return;

        Fade(_settingsPanel, true);
        // modal stack для настроек — это UI-уровень, не global pause
        if(ServiceLocator.TryGet<IInputService>(out var input))
            input.PushModal();
    }

    public void CloseSettings()
    {
        if(_settingsPanel == null || !SettingsOpen)
            return;

        Fade(_settingsPanel, false);
        if(ServiceLocator.TryGet<IInputService>(out var input))
            input.PopModal();
    }

    // --- Internal ---

    private void Fade(CanvasGroup cg, bool show)
    {
        if(cg == null)
            return;

        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeRoutine(cg, show, _fadeSeconds));
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, bool show, float seconds)
    {
        cg.blocksRaycasts = true;
        cg.interactable = show;

        float a0 = cg.alpha;
        float a1 = show ? 1f : 0f;
        float t = 0f;

        while(t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            cg.alpha = Mathf.Lerp(a0, a1, k);
            yield return null;
        }

        cg.alpha = a1;

        if(!show)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        _fadeRoutine = null;
    }

    private void ApplyPausedInstant(bool paused)
    {
        SetInstant(_pausePanel, paused);
    }

    private void CloseSettingsInstant()
    {
        SetInstant(_settingsPanel, false);
    }

    private static void SetInstant(CanvasGroup cg, bool show)
    {
        if(cg == null)
            return;

        cg.alpha = show ? 1f : 0f;
        cg.blocksRaycasts = show;
        cg.interactable = show;
    }
}
