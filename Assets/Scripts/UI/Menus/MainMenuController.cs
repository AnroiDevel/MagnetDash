using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _btnPlay;
    [SerializeField] private Button _btnContinue;
    [SerializeField] private Button _btnSettings;
    [SerializeField] private Button _btnExit;

    [Header("Panels")]
    [SerializeField] private CanvasGroup _settingsPanel;
    [SerializeField] private CanvasGroup _levelSelectPanel;

    [Header("Config")]
    [SerializeField] private int _firstLevelSceneIndex = 1;
    [SerializeField] private int _finalLevelSceneIndex = 13;

    private bool _isLoading;
    private IInputService _input;
    private IProgressService _progress;

    private void Awake()
    {
        if(_btnPlay != null)
            _btnPlay.onClick.AddListener(OnPlay);

        if(_btnContinue != null)
            _btnContinue.onClick.AddListener(OnContinue);

        if(_btnSettings != null)
            _btnSettings.onClick.AddListener(ToggleSettings);

        if(_btnExit != null)
            _btnExit.onClick.AddListener(OnExit);

#if UNITY_ANDROID || UNITY_IOS
        if(_btnExit != null)
            _btnExit.gameObject.SetActive(false);
#endif
    }

    private void OnEnable()
    {
        //if(ServiceLocator.TryGet(out _input))
        //{
        //    _input.Back += OnBack;
        //}

        ServiceLocator.WhenAvailable<IInputService>(i =>
        {
            _input = i;
            _input.Back += OnBack;
        });

        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;
            UpdateContinueButtonVisibility();
        });
    }

    private void OnDisable()
    {
        if(_input != null)
        {
            _input.Back -= OnBack;
            _input = null;
        }

        _progress = null;
    }

    private void Start()
    {
        UpdateContinueButtonVisibility();

        InitPanel(_settingsPanel, false);
        InitPanel(_levelSelectPanel, false);
    }


    private void UpdateContinueButtonVisibility()
    {
        if(_btnContinue == null)
            return;

        bool hasSave = false;

        if(ServiceLocator.TryGet<IProgressService>(out var progress))
        {
            _progress = progress;
            hasSave = _progress.TryGetLastCompletedLevel(out int last);
            _btnContinue.gameObject.SetActive(last < _finalLevelSceneIndex);
        }

    }

    // Обработка Cancel/Back из новой системы ввода
    private void OnBack()
    {
        // сначала закрываем настройки
        if(IsVisible(_settingsPanel))
        {
            ToggleSettings();
        }
        else if(IsVisible(_levelSelectPanel))
        {
            ToggleLevelSelect(false);
        }
    }

    private void InitPanel(CanvasGroup panel, bool visible)
    {
        if(panel == null)
            return;

        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    private void SetPanel(CanvasGroup panel, bool visible)
    {
        if(panel == null)
            return;

        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    private bool IsVisible(CanvasGroup panel)
    {
        return panel != null && panel.interactable; // или panel.alpha > 0.001f
    }

    // PLAY → открыть панель выбора уровней
    private void OnPlay()
    {
        ToggleLevelSelect(true);
    }

    private void ToggleSettings()
    {
        if(_settingsPanel == null)
            return;

        bool show = !IsVisible(_settingsPanel);
        SetPanel(_settingsPanel, show);

        if(_input != null)
        {
            if(show)
                _input.PushModal();
            else
                _input.PopModal();
        }
    }

    private void ToggleLevelSelect(bool? explicitState = null)
    {
        if(_levelSelectPanel == null)
            return;

        bool show = explicitState ?? !IsVisible(_levelSelectPanel);
        SetPanel(_levelSelectPanel, show);

        if(_input != null)
        {
            if(show)
                _input.PushModal();
            else
                _input.PopModal();
        }
    }

    // CONTINUE → загрузить последний уровень
    private void OnContinue()
    {
        if(_isLoading)
            return;

        int target = _firstLevelSceneIndex;

        if(_progress != null && _progress.TryGetLastCompletedLevel(out var last))
        {
            if(last >= _firstLevelSceneIndex && last < _finalLevelSceneIndex)
                target = last;
        }

        LoadLevelThroughFlow(target);
    }

    private void LoadLevelThroughFlow(int buildIndex)
    {
        if(_isLoading)
            return;

        _isLoading = true;

        // основной путь — через LevelManager / ServiceLocator
        if(ServiceLocator.TryGet<LevelManager>(out var levelManager))
        {
            levelManager.LoadLevel(buildIndex);
            _isLoading = false;
            return;
        }

        // запасной вариант — прямой LoadScene
        StartCoroutine(CoLoadSingleScene(buildIndex));
    }

    private IEnumerator CoLoadSingleScene(int buildIndex)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        while(!op.isDone)
            yield return null;

        _isLoading = false;
    }

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
