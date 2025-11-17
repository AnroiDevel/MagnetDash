using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PauseController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private CanvasGroup _panelPause;
    [SerializeField] private CanvasGroup _settingsPanel;

    [SerializeField] private float _fade = 0.15f;

    [SerializeField] private string _menuScene = "MainMenu";
    [SerializeField] private string _systemsScene = "Systems";

    public bool IsPaused { get; private set; }
    public event Action<bool> PausedChanged;

    private IInputService _input;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.Back += OnBack;      // для модалок
        });

    }

    private void OnDisable()
    {
        if(_input != null)
        {
            _input.Back -= OnBack;
        }
    }

    private bool SettingsOpen => _settingsPanel && _settingsPanel.alpha > 0;


    public void Toggle()
    {
        if(IsPaused)
            Resume();
        else
            Pause();
    }


    public void Pause()
    {

        if(IsPaused)
            return;
        IsPaused = true;
        Time.timeScale = 0f;
        _input?.EnableGameplay(false);    // отключаем геймплейный ввод
        Show(_panelPause, true);
        PausedChanged?.Invoke(true);

        Debug.Log(IsPaused);
    }


    public void Resume()
    {
        if(!IsPaused)
        {
            Pause();
            return;
        }

        if(SettingsOpen)
        { CloseSettings(); return; }
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
        if(_settingsPanel && _settingsPanel.alpha > 0)
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
        Resume();
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
        float a0 = cg.alpha, a1 = show ? 1f : 0f, tt = 0f;
        while(tt < t)
        { tt += Time.unscaledDeltaTime; cg.alpha = Mathf.Lerp(a0, a1, tt / t); yield return null; }
        cg.alpha = a1;
        if(!show)
        { cg.blocksRaycasts = false; cg.interactable = false; }
    }


    public void OnRestart()
    {
        Resume();
        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.Reload();
        }
        else
        {
            var lm = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Exclude);
            lm.Reload();
        }
    }

    public void OnExitToMenu()
    {
        StartCoroutine(CoGoToMenu());

    }


    private IEnumerator CoGoToMenu()
    {
        // 1) на всякий случай вернём время
        Time.timeScale = 1f;

        // 2) убедимся, что Systems загружена
        var systems = SceneManager.GetSceneByName(_systemsScene);
        if(!systems.IsValid() || !systems.isLoaded)
        {
            yield return SceneManager.LoadSceneAsync(_systemsScene, LoadSceneMode.Additive);
            systems = SceneManager.GetSceneByName(_systemsScene);
        }

        // 3) грузим главное меню аддитивно (если ещё не загружено)
        var menu = SceneManager.GetSceneByName(_menuScene);
        if(!menu.IsValid() || !menu.isLoaded)
            yield return SceneManager.LoadSceneAsync(_menuScene, LoadSceneMode.Additive);

        menu = SceneManager.GetSceneByName(_menuScene);
        SceneManager.SetActiveScene(menu);
        yield return null; // подождать один кадр, чтобы активная сцена применилась

        // 4) выгружаем все сцены, кроме Systems и MainMenu
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if(!s.isLoaded)
                continue;
            if(s == systems || s == menu)
                continue;
            yield return SceneManager.UnloadSceneAsync(s);
        }

        // (опционально) если в проекте иногда дублируется EventSystem,
        // оставим активным только тот, что в меню:
#if UNITY_UGUI
        var evt = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>
                 (FindObjectsSortMode.None);
        foreach (var es in evt)
            es.gameObject.SetActive(es.gameObject.scene == menu);
#endif
    }
}
