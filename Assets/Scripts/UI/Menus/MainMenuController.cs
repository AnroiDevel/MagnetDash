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
        if (_btnExit != null)
            _btnExit.gameObject.SetActive(false);
#endif
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IInputService>(input =>
        {
            _input = input;
            _input.Back += OnBack;
        });

        ServiceLocator.WhenAvailable<IProgressService>(p =>
        {
            _progress = p;
            _progress.Loaded += OnProgressLoaded;

            if(_progress.IsLoaded)
                OnProgressLoaded();
        });
    }

    private void OnDisable()
    {
        if(_input != null)
        {
            _input.Back -= OnBack;
            _input = null;
        }

        if(_progress != null)
        {
            _progress.Loaded -= OnProgressLoaded;
            _progress = null;
        }
    }

    private void Start()
    {
        UpdateContinueButtonVisibility();

        SetPanel(_settingsPanel, false);
        SetPanel(_levelSelectPanel, false);
    }


    private void OnProgressLoaded()
    {
        UpdateContinueButtonVisibility();
    }

    private void UpdateContinueButtonVisibility()
    {
        if(_btnContinue == null)
            return;

        bool show = false;

        // если сервис уже есть — используем его;
        // если нет — пытаемся получить, но не создаём жёсткую зависимость
        if(_progress != null || ServiceLocator.TryGet<IProgressService>(out _progress))
        {
            if(_progress.TryGetLastCompletedLevel(out int lastCompleted))
            {
                show = lastCompleted >= 0;
            }
        }

        _btnContinue.gameObject.SetActive(show);
    }

    public void OnBack()
    {
        if(IsVisible(_settingsPanel))
        {
            ToggleSettings();
        }
        else if(IsVisible(_levelSelectPanel))
        {
            ToggleLevelSelect(false);
        }
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
        return panel != null && panel.interactable;
    }

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

        if(_input == null)
            return;

        if(show)
            _input.PushModal();
        else
            _input.PopModal();
    }

    public void ToggleLevelSelect(bool? explicitState = null)
    {
        if(_levelSelectPanel == null)
            return;

        bool show = explicitState ?? !IsVisible(_levelSelectPanel);
        SetPanel(_levelSelectPanel, show);

        if(_input == null)
            return;

        if(show)
            _input.PushModal();
        else
            _input.PopModal();
    }

    private void OnContinue()
    {
        LoadLevelThroughFlow();
    }

    private void LoadLevelThroughFlow()
    {
        if(_isLoading)
            return;

        if(_progress == null && !ServiceLocator.TryGet<IProgressService>(out _progress))
            return;

        if(!_progress.TryGetLastCompletedLevel(out int lastCompleted))
            return;

        if(!ServiceLocator.TryGet<LevelManager>(out var levelManager))
            return;

        _isLoading = true;

        // хотим следующий за пройденным
        int targetIndex = lastCompleted + 1;

        levelManager.LoadByLogicalIndex(targetIndex);
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
