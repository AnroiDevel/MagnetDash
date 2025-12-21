using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour, ILevelFlow
{
    public event Action<LevelUiContext> LevelActivated;

    public readonly struct LevelUiContext
    {
        public readonly int LevelNumber;
        public readonly int ProgressKey;

        public LevelUiContext(int levelNumber, int progressKey)
        {
            LevelNumber = levelNumber;
            ProgressKey = progressKey;
        }
    }


    #region Serialized Fields

    [Header("Scenes / flow")]
    [SerializeField] private string _systemsSceneName = "Systems";
    [SerializeField] private int _menuSceneIndex = 0;
    [SerializeField] private int _firstLevelSceneIndex = 1;
    [SerializeField] private int _runtimeLevelSceneIndex = -1;
    [SerializeField, Min(1f)] private float _sceneOpTimeout = 15f;

    [Header("Economy")]
    [SerializeField] private EconomyConfig _economy;

    [Header("Stars")]
    [SerializeField, Min(0)] private int _starsPerLevel = 3;

    [Header("Engine wear")]
    [SerializeField, Range(0, 100)] private int _engineRepairThreshold = 70;

    #endregion

    #region State

    private LevelSceneFlow _sceneFlow;
    private LevelSessionController _sessionController;
    private LevelResultsPresenter _results;
    private LevelIndexResolver _index;

    private bool _isRuntimeLevel;
    private int _currentRuntimeJsonIndex = -1;

    #endregion

    #region Services

    private IProgressService _progress;
    private IGameFlowEvents _flowEvents;
    private IGameEvents _gameEvents;

    public GameState State { get; private set; } = GameState.Boot;

    #endregion

    #region Public API

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

        ServiceLocator.WhenAvailable<IProgressService>(OnProgressReady);
        ServiceLocator.WhenAvailable<IGameFlowEvents>(OnFlowEventsReady);
        ServiceLocator.WhenAvailable<IGameEvents>(OnGameEventsReady);

        _sceneFlow = new LevelSceneFlow(this, _systemsSceneName, _sceneOpTimeout, IsValidBuildIndex);
        _sessionController = new LevelSessionController(_starsPerLevel);
        _index = new LevelIndexResolver(_firstLevelSceneIndex, _runtimeLevelSceneIndex, SceneManager.sceneCountInBuildSettings);
    }

    private void Start()
    {
        var active = SceneManager.GetActiveScene();

        if(active.buildIndex == _runtimeLevelSceneIndex && !_isRuntimeLevel)
            Debug.LogWarning("[LevelManager] Runtime level scene запущена напрямую...");

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

    private void Update()
    {
        if(State != GameState.Playing)
            return;

        var session = _sessionController?.Session;
        if(session == null)
            return;

        _gameEvents?.FireTimeChanged(session.Elapsed);
    }

    private void OnDestroy()
    {
        _sceneFlow?.Cancel();

        ServiceLocator.Unsubscribe<IProgressService>(OnProgressReady);
        ServiceLocator.Unsubscribe<IGameFlowEvents>(OnFlowEventsReady);
        ServiceLocator.Unsubscribe<IGameEvents>(OnGameEventsReady);

        ServiceLocator.Unregister<ILevelFlow>(this);
        ServiceLocator.Unregister<LevelManager>(this);
    }

    private void OnProgressReady(IProgressService p)
    {
        _progress = p;
        RebuildPresentersIfReady();
    }

    private void OnFlowEventsReady(IGameFlowEvents e)
    {
        _flowEvents = e;
        RebuildPresentersIfReady();
    }

    private void OnGameEventsReady(IGameEvents e)
    {
        _gameEvents = e;
    }

    private void RebuildPresentersIfReady()
    {
        if(_results != null)
            return;

        if(_progress == null || _flowEvents == null)
            return;

        _results = new LevelResultsPresenter(_progress, _flowEvents, _starsPerLevel, _engineRepairThreshold, _economy);
    }

    #endregion

    #region Stars API

    public void CollectStar() => CollectStar(Vector3.zero);

    public void CollectStar(Vector3 worldPos)
    {
        if(State != GameState.Playing)
            return;

        _sessionController?.CollectStar();

        var session = _sessionController?.Session;
        if(session == null)
            return;

        _gameEvents?.FireStarCollected(session.CollectedStars, worldPos);
    }

    public int GetStarsCollected() => _sessionController?.Session?.CollectedStars ?? 0;
    public int GetStarsPerLevel() => _sessionController?.Session?.StarsPerLevel ?? _starsPerLevel;

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

        if(_isRuntimeLevel)
        {
            int nextJsonIndex = _currentRuntimeJsonIndex + 1;

            if(ServiceLocator.TryGet<IRuntimeLevelsConfig>(out var cfg))
            {
                if(nextJsonIndex >= cfg.LevelsCount)
                {
                    LoadMenu();
                    return;
                }
            }

            LoadRuntimeJsonLevel(nextJsonIndex);
            return;
        }

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

        var session = _sessionController?.Session;
        if(session == null || !IsValidBuildIndex(session.CurrentLevelBuildIndex))
        {
            Debug.LogError("[LevelManager] Invalid current level index in session.");
            return;
        }

        LoadSceneInternal(session.CurrentLevelBuildIndex);
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

    private void ShowWinResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = _index.GetProgressKey(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);
        int levelNo = _index.GetLevelNumberForUI(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);
        int logicalIndex = _index.GetLogicalLevelIndex(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);

        _results?.ShowWin(
            buildIndex,
            levelNo,
            progressKey,
            logicalIndex,
            _sessionController?.Session
        );
    }

    private void ShowFailResult()
    {
        var scene = SceneManager.GetActiveScene();
        int buildIndex = scene.buildIndex;

        int progressKey = _index.GetProgressKey(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);
        int levelNo = _index.GetLevelNumberForUI(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);

        _results?.ShowFail(
            buildIndex,
            levelNo,
            progressKey,
            _sessionController?.Session
        );
    }

    #endregion

    #region Scene Switching

    private void LoadSceneInternal(int buildIndex)
    {
        State = GameState.LoadingLevel;

        _sceneFlow.SwitchTo(
            buildIndex,
            onActivated: newlyLoaded =>
            {
                if(IsLevelScene(newlyLoaded))
                    InitLevelState(newlyLoaded.buildIndex);
            },
            onDone: success =>
            {
                if(!success)
                {
                    State = GameState.MainMenu;
                    Debug.LogWarning("[LevelManager] Scene switch finished with errors; state set to safe value.");
                    return;
                }

                if(buildIndex == _menuSceneIndex)
                    State = GameState.MainMenu;
                else if(IsValidBuildIndex(buildIndex) && buildIndex >= _firstLevelSceneIndex)
                    State = GameState.Playing;
                else
                    State = GameState.LevelSelect;
            }
        );
    }

    #endregion

    #region Helpers: scenes & indices

    private bool IsValidBuildIndex(int idx)
        => idx >= 0 && idx < SceneManager.sceneCountInBuildSettings;

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


    public void LoadByLogicalIndex(int logicalIndex)
    {
        if(State == GameState.LoadingLevel)
            return;

        if(!_index.TryResolveLogicalIndex(logicalIndex, out var target))
        {
            Debug.LogWarning($"[LevelManager] LoadByLogicalIndex: invalid index {logicalIndex}");
            return;
        }

        if(!target.isRuntime)
        {
            if(!IsValidBuildIndex(target.buildIndex))
            {
                Debug.LogError($"[LevelManager] LoadByLogicalIndex: invalid manual buildIndex {target.buildIndex}");
                return;
            }

            LoadLevel(target.buildIndex);
            return;
        }

        if(target.jsonIndex < 0)
        {
            Debug.LogError($"[LevelManager] LoadByLogicalIndex: invalid jsonIndex {target.jsonIndex}");
            return;
        }

        LoadRuntimeJsonLevel(target.jsonIndex);
    }


    private void InitLevelState(int buildIndex)
    {
        _sessionController.StartLevel(buildIndex);

        int levelNo = _index.GetLevelNumberForUI(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);
        int progressKey = _index.GetProgressKey(buildIndex, _isRuntimeLevel, _currentRuntimeJsonIndex);

        LevelActivated?.Invoke(new LevelUiContext(levelNo, progressKey));

        _gameEvents?.FireStarCollected(_sessionController.Session.CollectedStars, Vector3.zero);
    }


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
