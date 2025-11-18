using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private const string SystemsSceneName = "Systems";
    private const string KLastLevel = "last_level";

    [Header("Bindings")]
    [SerializeField] private Button _btnPlay;
    [SerializeField] private Button _btnContinue;
    [SerializeField] private Button _btnSettings;
    [SerializeField] private Button _btnExit;
    [SerializeField] private CanvasGroup _settingsPanel;

    [Header("Config")]
    [SerializeField] private int _firstLevelSceneIndex = 1;   // первая сцена-уровень в Build Settings

    private bool _isLoading;

    private void Awake()
    {
        // Кнопки
        //_btnPlay.onClick.AddListener(OnPlay);
        _btnContinue.onClick.AddListener(OnContinue);
        _btnSettings.onClick.AddListener(ToggleSettings);
        if(_btnExit)
            _btnExit.onClick.AddListener(OnExit);

#if UNITY_ANDROID || UNITY_IOS
        if (_btnExit) _btnExit.gameObject.SetActive(false); // мобильным Exit не показываем
#endif
    }

    private void Start()
    {
        // Активность Continue
        bool hasSave = PlayerPrefs.HasKey(KLastLevel);
        _btnContinue.interactable = hasSave;
    }

    private void Update()
    {
        // Назад: закрыть настройки, иначе — ничего
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(_settingsPanel && _settingsPanel.alpha > 0.5f)
                ToggleSettings();
        }
    }

    private void ToggleSettings()
    {
        if(!_settingsPanel)
            return;
        bool show = _settingsPanel.alpha < 0.5f;
        _settingsPanel.alpha = show ? 1f : 0f;
        _settingsPanel.blocksRaycasts = show;
        _settingsPanel.interactable = show;

        if(!_settingsPanel.gameObject.activeInHierarchy)
        {
            _settingsPanel.gameObject.SetActive(true);
        }
    }

    private void OnPlay()
    {
        if(_isLoading)
            return;
        int target = _firstLevelSceneIndex;
        StartCoroutine(CoLoadLevel(target));
    }

    private void OnContinue()
    {
        if(_isLoading)
            return;

        //int target = PlayerPrefs.GetInt(KLastLevel, _firstLevelSceneIndex);
        //// Фоллбек в допустимый диапазон
        //if(target < _firstLevelSceneIndex || target >= SceneManager.sceneCountInBuildSettings)
        //    target = _firstLevelSceneIndex;

        //StartCoroutine(CoLoadLevel(target));



        if(ServiceLocator.TryGet<LevelManager>(out var lm))
        {
            // Здесь можно реализовать свою логику "продолжения":
            // найти последний пройденный уровень по прогрессу и загрузить его.
            int target = PlayerPrefs.GetInt(KLastLevel, _firstLevelSceneIndex);
            // Фоллбек в допустимый диапазон
            if(target < _firstLevelSceneIndex || target >= SceneManager.sceneCountInBuildSettings)
                target = _firstLevelSceneIndex;
            lm.LoadLevel(target);
        }
        else
        {
            Debug.LogError("[MainMenuController] LevelManager service not found.");
        }
    }

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator CoLoadLevel(int buildIndex)
    {
        if(_isLoading)
            yield break;
        _isLoading = true;

        // 1) Гарантируем Systems
        var systems = SceneManager.GetSceneByName(SystemsSceneName);
        if(!systems.IsValid() || !systems.isLoaded)
            yield return SceneManager.LoadSceneAsync(SystemsSceneName, LoadSceneMode.Additive);

        // 2) Запоминаем текущую (меню)
        var previous = SceneManager.GetActiveScene();

        // 3) Грузим уровень аддитивно и делаем активным
        yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
        var level = SceneManager.GetSceneByBuildIndex(buildIndex);
        SceneManager.SetActiveScene(level);

        // 4) Выгружаем меню, если это не Systems и не только что загруженная
        if(previous.IsValid() && previous.isLoaded && previous.name != SystemsSceneName && previous != level)
            yield return SceneManager.UnloadSceneAsync(previous);

        _isLoading = false;
    }
}
