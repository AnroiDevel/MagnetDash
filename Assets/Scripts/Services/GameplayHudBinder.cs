using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIManager))]
public sealed class GameplayHudBinder : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private UIManager _ui;

    private IGameEvents _events;
    private IGameFlowEvents _flowEvents;
    private IProgressService _progress;
    private LevelManager _levelManager;

    private void Awake()
    {
        if(_ui == null)
            _ui = GetComponent<UIManager>();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IGameEvents>(BindGameEvents);
        ServiceLocator.WhenAvailable<IGameFlowEvents>(BindFlowEvents);
        ServiceLocator.WhenAvailable<IProgressService>(BindProgress);
        ServiceLocator.WhenAvailable<LevelManager>(BindLevelManager);
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<IGameEvents>(BindGameEvents);
        ServiceLocator.Unsubscribe<IGameFlowEvents>(BindFlowEvents);
        ServiceLocator.Unsubscribe<IProgressService>(BindProgress);
        ServiceLocator.Unsubscribe<LevelManager>(BindLevelManager);

        UnbindGameEvents();
        UnbindFlowEvents();
        UnbindProgress();
        UnbindLevelManager();
    }

    // -------- Bind / Unbind --------

    private void BindGameEvents(IGameEvents e)
    {
        if(ReferenceEquals(_events, e))
            return;

        UnbindGameEvents();
        _events = e;

        _events.PolarityChanged += OnPolarityChanged;
        _events.SpeedChanged += OnSpeedChanged;
        _events.StarCollected += OnStarCollected;
        _events.TimeChanged += OnTimeChanged;
        _events.PortalReached += OnPortalReached;
    }

    private void UnbindGameEvents()
    {
        if(_events == null)
            return;

        _events.PolarityChanged -= OnPolarityChanged;
        _events.SpeedChanged -= OnSpeedChanged;
        _events.StarCollected -= OnStarCollected;
        _events.TimeChanged -= OnTimeChanged;
        _events.PortalReached -= OnPortalReached;
        _events = null;
    }

    private void BindFlowEvents(IGameFlowEvents f)
    {
        if(ReferenceEquals(_flowEvents, f))
            return;

        UnbindFlowEvents();
        _flowEvents = f;

        _flowEvents.LevelCompleted += OnLevelCompleted;
        _flowEvents.LevelFailed += OnLevelFailed;
    }

    private void UnbindFlowEvents()
    {
        if(_flowEvents == null)
            return;

        _flowEvents.LevelCompleted -= OnLevelCompleted;
        _flowEvents.LevelFailed -= OnLevelFailed;
        _flowEvents = null;
    }

    private void BindProgress(IProgressService p)
    {
        if(ReferenceEquals(_progress, p))
            return;

        UnbindProgress();
        _progress = p;

        _progress.EngineDurabilityChanged += OnEngineDurabilityChanged;
    }

    private void UnbindProgress()
    {
        if(_progress == null)
            return;

        _progress.EngineDurabilityChanged -= OnEngineDurabilityChanged;
        _progress = null;
    }

    private void BindLevelManager(LevelManager lm)
    {
        if(ReferenceEquals(_levelManager, lm))
            return;

        UnbindLevelManager();
        _levelManager = lm;

        _levelManager.LevelActivated += OnLevelActivated;
    }

    private void UnbindLevelManager()
    {
        if(_levelManager == null)
            return;

        _levelManager.LevelActivated -= OnLevelActivated;
        _levelManager = null;
    }

    // -------- Level init --------

    private void OnLevelActivated(LevelManager.LevelUiContext ctx)
    {
        if(_ui == null)
            return;

        _ui.SetLevel(ctx.LevelNumber);
        _ui.RefreshBest(ctx.ProgressKey, _progress);

        _ui.SetStars(0);
        _ui.SetTime(0f);

        if(_progress != null && _progress.IsLoaded)
            _ui.UpdateEngineDangerIndicator(_progress.EngineDurability);
    }

    // -------- IGameEvents -> UI --------

    private void OnPolarityChanged(int sign)
    {
        if(_ui != null)
            _ui.OnPolarity(sign);
    }

    private void OnSpeedChanged(float speed)
    {
        if(_ui != null)
            _ui.SetSpeed(speed);
    }

    private void OnStarCollected(int collected, Vector3 _)
    {
        if(_ui != null)
            _ui.SetStars(collected);
    }

    private void OnTimeChanged(float t)
    {
        if(_ui != null)
            _ui.SetTime(t);
    }

    private void OnPortalReached() { }

    // -------- Progress -> UI --------

    private void OnEngineDurabilityChanged(int _)
    {
        if(_ui == null || _progress == null || !_progress.IsLoaded)
            return;

        _ui.UpdateEngineDangerIndicator(_progress.EngineDurability);
    }

    // -------- IGameFlowEvents -> UI --------

    private void OnLevelCompleted(LevelResultInfo info)
    {
        if(_ui == null)
            return;

        _ui.ShowWinToast(info.elapsedTime, info.isPersonalBest, info.collectedStars);
    }

    private void OnLevelFailed(LevelResultInfo info)
    {
        if(_ui == null)
            return;

        _ui.ShowFailToast(info.elapsedTime);
    }
}
