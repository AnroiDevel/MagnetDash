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

    [Header("Engine wear")]
    [SerializeField, Range(0, 100)] private int _engineRepairThreshold = 70;

    #endregion

    #region State
    private LevelSession _session;
    private Coroutine _sceneSwitchRoutine;


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
    private IGameEvents _gameEvents;

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
        ServiceLocator.WhenAvailable<IGameFlowEvents>(e => _flowEvents = e);
        ServiceLocator.WhenAvailable<IGameEvents>(e => _gameEvents = e);

        _session = new LevelSession(_starsPerLevel);
    }

    private void Start()
    {
        var active = SceneManager.GetActiveScene();

        if(active.buildIndex == _runtimeLevelSceneIndex && !_isRuntimeLevel)
        {
            Debug.LogWarning("[LevelManager] Runtime level scene запущена напрямую...");
        }

        if(IsLevelScene(active))
        {
            InitLevelState(active.buildIndex); // внутри можно вызывать _ui?., но _ui может быть null
            State = GameState.Playing;
        }
        else if(active.buildIndex == _menuSceneIndex)
        {
            State = GameState.MainMenu;
        }
    }


    private void Update()
    {
        if(State != GameState.Playing || _session == null)
            return;

        _gameEvents?.FireTimeChanged(_session.Elapsed);
    }


    private void OnDestroy()
    {
        ServiceLocator.Unregister<ILevelFlow>(this);
        ServiceLocator.Unregister<LevelManager>(this);
    }


    #endregion

    #region Stars API

    public void CollectStar()
    {
        CollectStar(Vector3.zero);
    }

    public void CollectStar(Vector3 worldPos)
    {
        if(State != GameState.Playing || _session == null)
            return;

        _session.CollectStar();

        // единый источник истины: считаем здесь и только отсюда шлём событие
        _gameEvents?.FireStarCollected(_session.CollectedStars, worldPos);
    }

    public int GetStarsCollected() => _session?.CollectedStars ?? 0;
    public int GetStarsPerLevel() => _session?.StarsPerLevel ?? _starsPerLevel;

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

        if(_session == null || !IsValidBuildIndex(_session.CurrentLevelBuildIndex))
        {
            Debug.LogError("[LevelManager] Invalid current level index in session.");
            return;
        }

        LoadSceneInternal(_session.CurrentLevelBuildIndex);
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

    public void Pause()
    {
        if(State != GameState.Playing)
            return;

        State = GameState.Paused;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if(State != GameState.Paused)
            return;

        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    #endregion

    #region Results

    private void TryShowEngineRepairUI()
    {
        Debug.Log($"[LevelManager] Using progress instance={_progress?.GetHashCode()} dur={_progress?.EngineDurability}");


        if(_progress == null || !_progress.IsLoaded)
            return;

        int durability = _progress.EngineDurability;
        if(durability > _engineRepairThreshold)
            return;

        int powerPercent = Mathf.RoundToInt(_progress.EnginePower * 100f);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
            ui.ShowEngineRepairOffer(powerPercent);
    }


    private void ShowWinResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = GetCurrentProgressKey(buildIndex);
        int levelNo = GetLevelNumberForUI(buildIndex);
        float elapsed = _session?.Elapsed ?? 0f;

        bool pb = _progress?.SetBestTimeIfBetter(progressKey, elapsed) ?? false;

        int collected = _session?.CollectedStars ?? 0;
        int starsPerLevel = _session?.StarsPerLevel ?? _starsPerLevel;
        int clampedStars = Mathf.Clamp(collected, 0, starsPerLevel);

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

        // <-- вот это добавляем
        int logicalIndex = GetLogicalLevelIndex(buildIndex);
        if(logicalIndex >= 0)
            _progress?.SetLastCompletedLevelIfHigher(logicalIndex);

        TryShowEngineRepairUI();
    }

    private void ShowFailResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = GetCurrentProgressKey(buildIndex);
        int levelNo = GetLevelNumberForUI(buildIndex);
        float elapsed = _session?.Elapsed ?? 0f;

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        var info = new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNo,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = _session?.CollectedStars ?? 0,
            isPersonalBest = false,
            isWin = false,
            hint = "Попробуй другую траекторию!"
        };

        _flowEvents?.FireLevelFailed(info);
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
    /// 0..∞ логический индекс:
    /// 0..manualCount-1  – ручные уровни
    /// manualCount..     – runtime JSON уровни
    /// </summary>
    private int GetLogicalLevelIndex(int sceneBuildIndex)
    {
        if(_isRuntimeLevel && _currentRuntimeJsonIndex >= 0)
        {
            int manualCount = GetManualLevelsCount();
            return manualCount + _currentRuntimeJsonIndex;
        }

        // ручные уровни
        return sceneBuildIndex - _firstLevelSceneIndex;
    }


    public void LoadByLogicalIndex(int logicalIndex)
    {
        if(State == GameState.LoadingLevel)
            return;

        if(logicalIndex < 0)
        {
            Debug.LogWarning($"[LevelManager] LoadByLogicalIndex: invalid index {logicalIndex}");
            return;
        }

        int manualCount = GetManualLevelsCount();

        // ручные уровни
        if(logicalIndex < manualCount)
        {
            int buildIndex = _firstLevelSceneIndex + logicalIndex;
            if(!IsValidBuildIndex(buildIndex))
            {
                Debug.LogError($"[LevelManager] LoadByLogicalIndex: invalid manual buildIndex {buildIndex}");
                return;
            }

            LoadLevel(buildIndex);
            return;
        }

        // runtime уровни
        int jsonIndex = logicalIndex - manualCount;
        if(jsonIndex < 0)
        {
            Debug.LogError($"[LevelManager] LoadByLogicalIndex: negative jsonIndex {jsonIndex}");
            return;
        }

        LoadRuntimeJsonLevel(jsonIndex);
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

    private void InitLevelState(int levelBuildIndex)
    {
        _session.StartLevel(levelBuildIndex);

        int levelNo = GetLevelNumberForUI(levelBuildIndex);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
        {
            ui.SetLevel(levelNo);

            int progressKey = GetCurrentProgressKey(levelBuildIndex);
            ui.RefreshBest(progressKey, _progress);

            ui.SetStars(_session.CollectedStars); // 0 / N
            ui.SetTime(0f);
        }

        // можно продублировать через события, если хочешь строго через binder:
        _gameEvents?.FireStarCollected(_session.CollectedStars, Vector3.zero);

        TryShowEngineRepairUI();

        if(_progress != null && ServiceLocator.TryGet<IUIService>(out var ui2))
            ui2.UpdateEngineDangerIndicator(_progress.EngineDurability);
    }


    private void LoadSceneInternal(int buildIndex)
    {
        if(_sceneSwitchRoutine != null)
            StopCoroutine(_sceneSwitchRoutine);

        State = GameState.LoadingLevel;
        _sceneSwitchRoutine = StartCoroutine(CoSwitchToScene(buildIndex));
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
