using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет прогрессией уровней, временем и звёздами.
/// Живёт в сцене Systems и не уничтожается между загрузками.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour
{
    [Header("Scenes / flow")]
    [SerializeField] private string _systemsSceneName = "Systems";
    [SerializeField] private int _menuBuildIndex = 0;  // билд-индекс главного меню
    [SerializeField] private int _firstLevelBuildIdx = 1;  // первая сцена уровня в Build Settings
    [SerializeField] private float _autoNextDelay = 1.2f;
    [SerializeField, Min(1f)] private float _sceneOpTimeout = 15f;

    [Header("Stars")]
    [SerializeField, Min(0)] private int _starsPerLevel = 3;  // фиксированное число звёзд на уровень
    private int _starsCollected;

    // UI результатов уровня (регистрируется самим LevelResultPanel)
    private LevelResultPanel _resultPanel;

    // состояние попытки
    private float _levelStartTime;
    private bool _won;
    private bool _isLoading;

    // сервисы
    private IProgressService _progress;
    private IUIService _ui;

    private void Awake()
    {
        // Доступ к менеджеру для других компонентов
        ServiceLocator.Register<LevelManager>(this);

        ServiceLocator.WhenAvailable<IProgressService>(p => _progress = p);
        ServiceLocator.WhenAvailable<IUIService>(ui => _ui = ui);

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // На случай, если при старте активна уже сцена уровня (а не меню)
        var active = SceneManager.GetActiveScene();
        if(IsLevelScene(active))
            InitLevelState(active);
    }

    private void OnDestroy()
    {
        // На всякий случай отцепим панель
        if(_resultPanel != null)
        {
            _resultPanel.RetryRequested -= OnResultRetry;
            _resultPanel.NextRequested -= OnResultNext;
            _resultPanel.MenuRequested -= OnResultMenu;
        }

        ServiceLocator.Unregister<LevelManager>(this);
    }

    private void Update()
    {
        var active = SceneManager.GetActiveScene();
        if(_won || !IsLevelScene(active))
            return;

        _ui?.SetTime(Time.time - _levelStartTime);
    }

    // ===== Публичные методы, которыми пользуются другие компоненты =====

    /// <summary>Регистрация панели результатов. Вызывает сама панель при появлении.</summary>
    public void RegisterResultPanel(LevelResultPanel panel)
    {
        if(panel == null)
            return;

        // Отписываемся от старой, если была
        if(_resultPanel != null)
        {
            _resultPanel.RetryRequested -= OnResultRetry;
            _resultPanel.NextRequested -= OnResultNext;
            _resultPanel.MenuRequested -= OnResultMenu;
        }

        _resultPanel = panel;
        _resultPanel.Hide();
        _resultPanel.RetryRequested += OnResultRetry;
        _resultPanel.NextRequested += OnResultNext;
        _resultPanel.MenuRequested += OnResultMenu;
    }

    /// <summary>Отвязка панели. Вызывает сама панель при уничтожении/выключении.</summary>
    public void UnregisterResultPanel(LevelResultPanel panel)
    {
        if(panel == null || panel != _resultPanel)
            return;

        _resultPanel.RetryRequested -= OnResultRetry;
        _resultPanel.NextRequested -= OnResultNext;
        _resultPanel.MenuRequested -= OnResultMenu;
        _resultPanel = null;
    }

    public void CollectStar()
    {
        if(_won)
            return;

        if(_starsCollected < _starsPerLevel)
            _starsCollected++;
    }

    public int GetStarsCollected() => _starsCollected;
    public int GetStarsPerLevel() => _starsPerLevel;

    public void KillPlayer()
    {
        if(!IsLevelScene(SceneManager.GetActiveScene()))
            return;

        _ui?.ShowFailToast();
        Reload();
    }

    public void CompleteLevel()
    {
        if(_won)
            return;
        _won = true;

        var scene = SceneManager.GetActiveScene();
        int build = scene.buildIndex;
        int levelNo = build - _firstLevelBuildIdx + 1;

        float elapsed = Time.time - _levelStartTime;

        bool pb = _progress?.SetBestTimeIfBetter(build, elapsed) ?? false;
        _progress?.SetStarsMax(build, _starsCollected);

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(build, out var bestTime))
            best = bestTime;

        // подсказка (потом можно взять из конфигурации уровня)
        string hint = "Используй обе полярности!";

        if(_resultPanel != null)
        {
            _resultPanel.ShowWin(
                levelNo,
                elapsed,
                best,
                _starsCollected,
                pb,
                hint);
        }
        else
        {
            // fallback, если панели нет
            _ui?.ShowWinToast(elapsed, pb, _starsCollected);
            Invoke(nameof(LoadNext), _autoNextDelay);
        }
    }

    // ===== Обработчики событий панели =====

    private void OnResultRetry() => Reload();
    private void OnResultNext() => LoadNext();
    private void OnResultMenu() => LoadMainMenu();

    // ===== Навигация по сценам (публичные методы) =====

    public void LoadNext()
    {
        if(_isLoading)
            return;

        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        int nextIndex = current.buildIndex + 1;
        if(!IsValidBuildIndex(nextIndex))
            nextIndex = _firstLevelBuildIdx;

        if(!IsValidBuildIndex(nextIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _firstLevelBuildIdx={_firstLevelBuildIdx}");
            return;
        }

        StartCoroutine(CoSwitchToScene(nextIndex));
    }

    public void Reload()
    {
        if(_isLoading)
            return;

        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        int idx = current.buildIndex;
        if(!IsValidBuildIndex(idx))
        {
            Debug.LogError($"[LevelManager] Invalid active buildIndex={idx}");
            return;
        }

        StartCoroutine(CoSwitchToScene(idx));
    }

    public void LoadMainMenu()
    {
        if(_isLoading)
            return;

        if(!IsValidBuildIndex(_menuBuildIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _menuBuildIndex={_menuBuildIndex}");
            return;
        }

        StartCoroutine(CoSwitchToScene(_menuBuildIndex));
    }

    // ===== Внутренние вспомогательные методы =====

    private bool IsValidBuildIndex(int idx)
    {
        return idx >= 0 && idx < SceneManager.sceneCountInBuildSettings;
    }

    private bool IsLevelScene(Scene scene)
    {
        if(!scene.IsValid())
            return false;
        if(scene.name == _systemsSceneName)
            return false;
        if(scene.buildIndex == _menuBuildIndex)
            return false;
        return scene.buildIndex >= _firstLevelBuildIdx;
    }

    /// <summary>Инициализация состояния при входе в сцену уровня.</summary>
    private void InitLevelState(Scene scene)
    {
        if(!IsLevelScene(scene))
            return;

        _levelStartTime = Time.time;
        _won = false;
        _isLoading = false;
        _starsCollected = 0;

        int levelNumber = scene.buildIndex - _firstLevelBuildIdx + 1;
        _ui?.SetLevel(levelNumber);
        _ui?.SetTime(0f);

        if(_progress != null && _progress.TryGetBestTime(scene.buildIndex, out var best))
            _ui?.RefreshBest(best);
        else
            _ui?.RefreshBest(null);
    }

    // ===== Переключение сцен (Systems + одна контентная) =====

    private IEnumerator CoSwitchToScene(int targetBuildIndex)
    {
        if(_isLoading)
            yield break;

        _isLoading = true;
        bool success = false;

        try
        {
            if(!IsValidBuildIndex(targetBuildIndex))
            {
                Debug.LogError($"[LevelManager] Target buildIndex {targetBuildIndex} is invalid.");
                yield break;
            }

            var current = SceneManager.GetActiveScene();

            // 1) Гарантируем, что Systems загружена
            var systems = SceneManager.GetSceneByName(_systemsSceneName);
            if(!systems.IsValid() || !systems.isLoaded)
            {
                var sysOp = SceneManager.LoadSceneAsync(_systemsSceneName, LoadSceneMode.Additive);
                if(sysOp == null)
                {
                    Debug.LogError($"[LevelManager] Failed to start loading Systems '{_systemsSceneName}'.");
                    yield break;
                }

                float tSys = 0f;
                while(!sysOp.isDone)
                {
                    tSys += Time.unscaledDeltaTime;
                    if(tSys > _sceneOpTimeout)
                    {
                        Debug.LogError($"[LevelManager] Timeout while loading Systems '{_systemsSceneName}'.");
                        yield break;
                    }
                    yield return null;
                }

                systems = SceneManager.GetSceneByName(_systemsSceneName);
            }

            // 2) Грузим целевую сцену и запоминаем именно НОВУЮ
            Scene newlyLoaded = default;

            void OnLoaded(Scene s, LoadSceneMode mode)
            {
                if(s.buildIndex == targetBuildIndex && s != systems)
                    newlyLoaded = s;
            }

            SceneManager.sceneLoaded += OnLoaded;

            var loadOp = SceneManager.LoadSceneAsync(targetBuildIndex, LoadSceneMode.Additive);
            if(loadOp == null)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                Debug.LogError($"[LevelManager] Failed to start loading scene index {targetBuildIndex}.");
                yield break;
            }

            float tLoad = 0f;
            while(!loadOp.isDone)
            {
                tLoad += Time.unscaledDeltaTime;
                if(tLoad > _sceneOpTimeout)
                {
                    SceneManager.sceneLoaded -= OnLoaded;
                    Debug.LogError($"[LevelManager] Timeout while loading scene {targetBuildIndex}.");
                    yield break;
                }
                yield return null;
            }

            SceneManager.sceneLoaded -= OnLoaded;

            // fallback: если по какой-то причине обработчик не зацепил сцену
            if(!newlyLoaded.IsValid())
            {
                for(int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if(s.isLoaded && s.buildIndex == targetBuildIndex && s != systems)
                    {
                        newlyLoaded = s;
                        break;
                    }
                }
            }

            if(!newlyLoaded.IsValid())
            {
                Debug.LogError($"[LevelManager] Could not identify newly loaded scene {targetBuildIndex}.");
                yield break;
            }

            // 3) Активируем новую сцену и инициализируем состояние, если это уровень
            SceneManager.SetActiveScene(newlyLoaded);
            yield return null; // дать кадр на Awake / OnEnable UI
            InitLevelState(newlyLoaded);

            // 4) Собираем и выгружаем все сцены, кроме Systems и новой
            var toUnload = new List<Scene>(SceneManager.sceneCount);
            for(int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if(!s.isLoaded)
                    continue;
                if(s == newlyLoaded)
                    continue;
                if(s == systems)
                    continue;
                toUnload.Add(s);
            }

            foreach(var s in toUnload)
            {
                var unOp = SceneManager.UnloadSceneAsync(s);
                if(unOp == null)
                {
                    Debug.LogWarning($"[LevelManager] Unload op null for scene '{s.name}'. Skipping.");
                    continue;
                }

                float tUn = 0f;
                while(!unOp.isDone)
                {
                    tUn += Time.unscaledDeltaTime;
                    if(tUn > _sceneOpTimeout)
                    {
                        Debug.LogError($"[LevelManager] Timeout while unloading scene '{s.name}'. Continue.");
                        break;
                    }
                    yield return null;
                }
            }

            success = true;
        }
        finally
        {
            _isLoading = false;
            if(!success)
                Debug.LogWarning("[LevelManager] Scene switch finished with errors; state reset.");
        }
    }
}
