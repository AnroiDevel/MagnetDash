using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayHudBinder : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private UIManager _ui;

    private IGameEvents _events;
    private IGameFlowEvents _flowEvents;

    private void Awake()
    {
        if(_ui == null)
            _ui = GetComponent<UIManager>();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IGameEvents>(e =>
        {
            _events = e;
            _events.PolarityChanged += OnPolarityChanged;
            _events.SpeedChanged += OnSpeedChanged;
            _events.StarCollected += OnStarCollected;
            _events.TimeChanged += OnTimeChanged;
            _events.PortalReached += OnPortalReached;
        });

        ServiceLocator.WhenAvailable<IGameFlowEvents>(f =>
        {
            _flowEvents = f;
            _flowEvents.LevelCompleted += OnLevelCompleted;
            _flowEvents.LevelFailed += OnLevelFailed;
        });
    }

    private void OnDisable()
    {
        if(_events != null)
        {
            _events.PolarityChanged -= OnPolarityChanged;
            _events.SpeedChanged -= OnSpeedChanged;
            _events.StarCollected -= OnStarCollected;
            _events.TimeChanged -= OnTimeChanged;
            _events.PortalReached -= OnPortalReached;
            _events = null;
        }

        if(_flowEvents != null)
        {
            _flowEvents.LevelCompleted -= OnLevelCompleted;
            _flowEvents.LevelFailed -= OnLevelFailed;
            _flowEvents = null;
        }
    }

    // --- IGameEvents → UI ---

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

    private void OnStarCollected(int collected, int perLevel)
    {
        if(_ui != null)
            _ui.SetStars(collected, perLevel);
    }

    private void OnTimeChanged(float t)
    {
        if(_ui != null)
            _ui.SetTime(t);
    }

    private void OnPortalReached()
    {
        // опционально
    }

    // --- IGameFlowEvents → UI (тосты) ---

    private void OnLevelCompleted(LevelResultInfo info)
    {
        if(_ui == null)
            return;

        _ui.ShowWinToast(
            info.elapsedTime,
            info.isPersonalBest,
            info.collectedStars
        );
    }

    private void OnLevelFailed(LevelResultInfo info)
    {
        if(_ui == null)
            return;

        _ui.ShowFailToast(info.elapsedTime);
    }
}
