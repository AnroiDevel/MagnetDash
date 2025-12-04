using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет прогрессией уровней, временем и звёздами.
/// Живёт в сцене Systems и не уничтожается между загрузками.
/// Поддерживает два типа уровней:
/// - ручные сцены (BuildSettings)
/// - runtime JSON уровни внутри одной сцены (_runtimeLevelSceneIndex)
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour, ILevelFlow
{
    #region Serialized Fields

    [Header("Scenes / flow")]
    [SerializeField] private string _systemsSceneName = "Systems";
    [SerializeField] private int _menuSceneIndex = 0;           // билд-индекс главного меню
    [SerializeField] private int _firstLevelSceneIndex = 1;     // первая сцена уровня в Build Settings
    [SerializeField] private int _runtimeLevelSceneIndex = -1;  // сцена с LevelRuntimeLoader
    [SerializeField, Min(1f)] private float _sceneOpTimeout = 15f;

    [Header("Stars")]
    [SerializeField, Min(0)] private int _starsPerLevel = 3;    // фиксированное число звёзд на уровень

    #endregion

    #region State

    private int _collectedStars;

    private float _levelStartTime;
    private int _currentLevelIndex;          // билд-индекс активной сцены уровня

    // runtime JSON
    private bool _isRuntimeLevel;
    private int _currentRuntimeJsonIndex = -1;   // индекс в levels.json (0..N-1)

    // кеш количества ручных уровней (для UI)
    private int _manualLevelsCountCached = -1;

    #endregion

    #region Services

    private IProgressService _progress;
    private IUIService _ui;
    private IGameFlowEvents _flowEvents;

    public GameState State { get; private set; } = GameState.Boot;

    #endregion

    #region Public API (runtime info)

    public bool IsRuntimeLevel => _isRuntimeLevel;

    public bool TryGetCurrentRuntimeJsonIndex(out int jsonIndex)
    {
        if(!_isRuntimeLevel || _currentRuntimeJsonIndex < 0)
        {
            jsonIndex = -1;
            return false;
        }

        jsonIndex = _currentRuntimeJsonIndex;
        return true;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ServiceLocator.Register<LevelManager>(this);
        ServiceLocator.Register<ILevelFlow>(this);

        ServiceLocator.WhenAvailable<IProgressService>(p => _progress = p);
        ServiceLocator.WhenAvailable<IUIService>(ui => _ui = ui);
        ServiceLocator.WhenAvailable<IGameFlowEvents>(e => _flowEvents = e);
    }

    private void Start()
    {
        var active = SceneManager.GetActiveScene();

        // Если стартуем в runtime-сцене напрямую из редактора — _isRuntimeLevel будет false,
        // и уровень будет вести себя как ручной. Это ок для дебага, но даём явное предупреждение.
        if(active.buildIndex == _runtimeLevelSceneIndex && !_isRuntimeLevel)
        {
            Debug.LogWarning(
                "[LevelManager] Runtime level scene запущена напрямую. " +
                "Для корректного режима JSON уровней загружай их через LoadRuntimeJsonLevel().");
        }

        if(IsLevelScene(active))
        {
            InitLevelState(active.buildIndex);
            State = GameState.Playing;
        }
        else if(active.buildIndex == _menuSceneIndex)
        {
            State = GameState.MainMenu;
        }
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ILevelFlow>(this);
        ServiceLocator.Unregister<LevelManager>(this);
    }

    private void Update()
    {
        if(State != GameState.Playing)
            return;

        var active = SceneManager.GetActiveScene();
        if(!IsLevelScene(active))
            return;

        _ui?.SetTime(Time.time - _levelStartTime);
    }

    #endregion

    #region Stars API

    public void CollectStar()
    {
        if(State != GameState.Playing)
            return;

        if(_collectedStars < _starsPerLevel)
            _collectedStars++;
    }

    public int GetStarsCollected() => _collectedStars;
    public int GetStarsPerLevel() => _starsPerLevel;

    #endregion

    #region ILevelFlow

    public void CompleteLevel()
    {
        if(State != GameState.Playing)
            return;

        State = GameState.LevelCompleted;
        ShowWinResult();
    }

    public void KillPlayer()
    {
        if(State != GameState.Playing)
            return;

        State = GameState.LevelFailed;
        ShowFailResult();
    }

    public void LoadNext()
    {
        if(State != GameState.LevelCompleted)
            return;

        // --- ветка для runtime JSON уровней ---
        if(_isRuntimeLevel)
        {
            int nextJsonIndex = _currentRuntimeJsonIndex + 1;

            if(ServiceLocator.TryGet<IRuntimeLevelsConfig>(out var cfg))
            {
                int total = cfg.LevelsCount;
                if(nextJsonIndex >= total)
                {
                    // JSON-уровни закончились — идём в меню (или можно показать финал)
                    LoadMenu();
                    return;
                }
            }

            LoadRuntimeJsonLevel(nextJsonIndex);
            return;
        }

        // --- старая логика для ручных сцен ---
        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        int nextIndex = current.buildIndex + 1;
        if(!IsValidBuildIndex(nextIndex))
            nextIndex = _firstLevelSceneIndex;

        if(!IsValidBuildIndex(nextIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _firstLevelSceneIndex={_firstLevelSceneIndex}");
            return;
        }

        LoadSceneInternal(nextIndex);
    }

    public void Reload()
    {
        if(State != GameState.Paused &&
           State != GameState.LevelFailed &&
           State != GameState.LevelCompleted)
            return;

        var current = SceneManager.GetActiveScene();
        if(!IsLevelScene(current))
            return;

        if(!IsValidBuildIndex(_currentLevelIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _currentLevelIndex={_currentLevelIndex}");
            return;
        }

        LoadSceneInternal(_currentLevelIndex);
    }

    public void LoadLevel(int buildIndex)
    {
        _isRuntimeLevel = false;
        _currentRuntimeJsonIndex = -1;

        LoadSceneInternal(buildIndex);
    }

    public void LoadMenu()
    {
        if(State == GameState.LoadingLevel)
            return;

        if(!IsValidBuildIndex(_menuSceneIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _menuSceneIndex={_menuSceneIndex}");
            return;
        }

        _isRuntimeLevel = false;
        _currentRuntimeJsonIndex = -1;

        LoadSceneInternal(_menuSceneIndex);
    }

    /// <summary>
    /// Загрузить runtime JSON-уровень по индексу в levels.json (0..N-1).
    /// </summary>
    public void LoadRuntimeJsonLevel(int jsonIndex)
    {
        if(State == GameState.LoadingLevel)
            return;

        if(_runtimeLevelSceneIndex < 0 || !IsValidBuildIndex(_runtimeLevelSceneIndex))
        {
            Debug.LogError($"[LevelManager] Invalid _runtimeLevelSceneIndex={_runtimeLevelSceneIndex}");
            return;
        }

        _currentRuntimeJsonIndex = jsonIndex;
        _isRuntimeLevel = true;

        LoadSceneInternal(_runtimeLevelSceneIndex);
    }

    internal void Pause()
    {
        if(State != GameState.Playing)
            return;

        State = GameState.Paused;
    }

    internal void Resume()
    {
        if(State != GameState.Paused)
            return;

        State = GameState.Playing;
    }

    #endregion

    #region Results

    private void ShowWinResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = GetCurrentProgressKey(buildIndex);
        int levelNo = GetLevelNumberForUI(buildIndex);
        float elapsed = Time.time - _levelStartTime;

        bool pb = _progress?.SetBestTimeIfBetter(progressKey, elapsed) ?? false;
        int clampedStars = Mathf.Clamp(_collectedStars, 0, _starsPerLevel);
        if(clampedStars > 0)
            _progress?.SetStarsMax(progressKey, clampedStars);

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        var info = new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNo,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = clampedStars,
            isPersonalBest = pb,
            isWin = true,
            hint = "Используй обе полярности!"
        };

        _flowEvents?.FireLevelCompleted(info);
        _ui?.ShowWinToast(elapsed, pb, clampedStars);
    }

    private void ShowFailResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = GetCurrentProgressKey(buildIndex);
        int levelNo = GetLevelNumberForUI(buildIndex);
        float elapsed = Time.time - _levelStartTime;

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        var info = new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNo,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = _collectedStars,
            isPersonalBest = false,
            isWin = false,
            hint = "Попробуй другую траекторию!"
        };

        _flowEvents?.FireLevelFailed(info);
        _ui?.ShowFailToast(elapsed);
    }

    #endregion

    #region Helpers: scenes & indices

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
        if(scene.buildIndex == _menuSceneIndex)
            return false;
        return scene.buildIndex >= _firstLevelSceneIndex;
    }

    /// <summary>
    /// Ключ для прогресса:
    /// - ручные уровни: buildIndex
    /// - JSON уровни: отрицательные id (0 -> -1, 1 -> -2, ...)
    /// </summary>
    private int GetCurrentProgressKey(int sceneBuildIndex)
    {
        if(_isRuntimeLevel && _currentRuntimeJsonIndex >= 0)
        {
            return -1 - _currentRuntimeJsonIndex;
        }

        return sceneBuildIndex;
    }

    /// <summary>Инициализация состояния при входе в сцену уровня.</summary>
    private void InitLevelState(int levelBuildIndex)
    {
        _currentLevelIndex = levelBuildIndex;
        _collectedStars = 0;
        _levelStartTime = Time.time;

        int levelNo = GetLevelNumberForUI(levelBuildIndex);
        _ui?.SetLevel(levelNo);

        int progressKey = GetCurrentProgressKey(levelBuildIndex);
        _ui?.RefreshBest(progressKey, _progress);
    }

    private void LoadSceneInternal(int buildIndex)
    {
        StopAllCoroutines();
        State = GameState.LoadingLevel;
        StartCoroutine(CoSwitchToScene(buildIndex));
    }

    private IEnumerator CoSwitchToScene(int targetBuildIndex)
    {
        bool success = false;

        try
        {
            if(!IsValidBuildIndex(targetBuildIndex))
            {
                Debug.LogError($"[LevelManager] Target buildIndex {targetBuildIndex} is invalid.");
                yield break;
            }

            var current = SceneManager.GetActiveScene();

            // 1) гарантируем, что Systems загружена
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

            // 2) грузим целевую сцену
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

            // fallback
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

            // 3) активируем новую сцену и инициализируем состояние уровня
            SceneManager.SetActiveScene(newlyLoaded);
            yield return null; // дать кадр на Awake / OnEnable UI

            if(IsLevelScene(newlyLoaded))
                InitLevelState(newlyLoaded.buildIndex);

            // 4) выгружаем всё, кроме Systems и новой сцены
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
            if(targetBuildIndex == _menuSceneIndex)
                State = GameState.MainMenu;
            else if(IsValidBuildIndex(targetBuildIndex) && targetBuildIndex >= _firstLevelSceneIndex)
                State = GameState.Playing;
            else
                State = GameState.LevelSelect;

            if(!success)
                Debug.LogWarning("[LevelManager] Scene switch finished with errors; state reset.");
        }
    }

    #endregion

    #region Helpers: UI numbering

    /// <summary>
    /// Количество ручных уровней по BuildSettings.
    /// Если задана runtime-сцена, считаем, что ручные — это диапазон
    /// [ _firstLevelSceneIndex .. _runtimeLevelSceneIndex ).
    /// Иначе — до конца списка сцен.
    /// </summary>
    private int GetManualLevelsCount()
    {
        if(_manualLevelsCountCached >= 0)
            return _manualLevelsCountCached;

        int totalScenes = SceneManager.sceneCountInBuildSettings;

        if(IsValidBuildIndex(_runtimeLevelSceneIndex) &&
           _runtimeLevelSceneIndex > _firstLevelSceneIndex)
        {
            _manualLevelsCountCached =
                Mathf.Max(0, _runtimeLevelSceneIndex - _firstLevelSceneIndex);
        }
        else
        {
            _manualLevelsCountCached =
                Mathf.Max(0, totalScenes - _firstLevelSceneIndex);
        }

        return _manualLevelsCountCached;
    }

    /// <summary>
    /// Номер уровня для UI:
    /// - ручные сцены: buildIndex - _firstLevelSceneIndex + 1
    /// - runtime JSON уровни: (кол-во ручных) + jsonIndex + 1
    /// </summary>
    private int GetLevelNumberForUI(int sceneBuildIndex)
    {
        if(_isRuntimeLevel && _currentRuntimeJsonIndex >= 0)
        {
            int manualCount = GetManualLevelsCount();
            return manualCount + _currentRuntimeJsonIndex + 1;
        }

        return sceneBuildIndex - _firstLevelSceneIndex + 1;
    }


    /// <summary>
    /// Регистрация активного JSON-уровня при запуске runtime-сцены напрямую из редактора.
    /// Нужна только для дебага: делает уровень "runtime" в глазах LevelManager.
    /// </summary>
    public void RegisterRuntimeJsonIndex(int jsonIndex)
    {
        if(jsonIndex < 0)
        {
            Debug.LogWarning($"[LevelManager] RegisterRuntimeJsonIndex: invalid index {jsonIndex}");
            return;
        }

        _currentRuntimeJsonIndex = jsonIndex;
        _isRuntimeLevel = true;
    }

    #endregion
}
