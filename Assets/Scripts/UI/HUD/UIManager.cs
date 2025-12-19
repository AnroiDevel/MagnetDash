using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour, IUIService
{
    [Header("Texts")]
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _bestText;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _toastText;

    [SerializeField] private Image[] _starIcons; // длина всегда 3
    [SerializeField] private TMP_Text _starCountText;
    [SerializeField] private Color _filledColor = Color.white;
    [SerializeField] private Color _emptyColor = new(1, 1, 1, 0.25f);

    [Header("Toast")]
    [SerializeField] private float _defaultToastSeconds = 1.0f;

    [Header("Engine repair")]
    [SerializeField] private EngineRepairPanel _engineRepairPanel;

    [Header("Engine danger")]
    [SerializeField] private Button _dangerBtn;
    [SerializeField] private Image _dagerIcon; // можно потом переименовать через FormerlySerializedAs
    [SerializeField] private Color _lowDangerColor = Color.yellow;
    [SerializeField] private Color _highDangerColor = Color.red;
    [SerializeField, Range(0, 100)] private int _dangerLowThreshold = 70;
    [SerializeField, Range(0, 100)] private int _dangerHighThreshold = 40;

    private Coroutine _toastRoutine;
    private IGameEvents _gameEvents;

    public event System.Action<string> ToastShown;

    private void Awake()
    {
        ServiceLocator.Register<IUIService>(this);
        ServiceLocator.WhenAvailable<IGameEvents>(OnGameEventsAvailable);

        if(_toastText)
            _toastText.gameObject.SetActive(false);

        if(_dangerBtn != null)
        {
            _dangerBtn.onClick.AddListener(OnDangerClicked);
            _dangerBtn.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if(_dangerBtn != null)
            _dangerBtn.onClick.RemoveListener(OnDangerClicked);

        if(_gameEvents != null)
        {
            _gameEvents.PolarityChanged -= HandlePolarityChanged;
            _gameEvents.SpeedChanged -= HandleSpeedChanged;
            _gameEvents.StarCollected -= HandleStarCollected;
            _gameEvents.TimeChanged -= HandleTimeChanged;
        }

        ServiceLocator.Unsubscribe<IGameEvents>(OnGameEventsAvailable);
        ServiceLocator.Unregister<IUIService>(this);
    }


    private void OnGameEventsAvailable(IGameEvents events)
    {
        // на случай повторного вызова WhenAvailable
        if(_gameEvents != null)
        {
            _gameEvents.PolarityChanged -= HandlePolarityChanged;
            _gameEvents.SpeedChanged -= HandleSpeedChanged;
            _gameEvents.StarCollected -= HandleStarCollected;
            _gameEvents.TimeChanged -= HandleTimeChanged;
        }

        _gameEvents = events;

        if(_gameEvents == null)
            return;

        _gameEvents.PolarityChanged += HandlePolarityChanged;
        _gameEvents.SpeedChanged += HandleSpeedChanged;
        _gameEvents.StarCollected += HandleStarCollected;
        _gameEvents.TimeChanged += HandleTimeChanged;
    }

    // ---------- IUIService ----------

    public void SetStars(int collected)
    {
        if(_starIcons != null)
        {
            for(int i = 0; i < _starIcons.Length; i++)
            {
                if(_starIcons[i] == null)
                    continue;

                bool filled = i < collected;
                _starIcons[i].color = filled ? _filledColor : _emptyColor;
            }
        }

        if(_starCountText != null)
            _starCountText.text = collected.ToString();
    }

    public void SetLevel(int level)
    {
        if(_levelText)
            _levelText.text = $"Level {level}";
    }

    public void SetTime(float seconds)
    {
        if(_timeText)
            _timeText.text = $"Time: {Format(seconds)}";
    }

    public void RefreshBest(int progressKey, IProgressService progress)
    {
        if(!_bestText)
            return;

        bool has = false;
        float best;

        if(progress != null && progress.TryGetBestTime(progressKey, out best))
        {
            has = float.IsFinite(best);
        }
        else
        {
            best = 0f;
        }

        _bestText.gameObject.SetActive(has);

        if(has)
            _bestText.text = $"Best: {Format(best)}";
    }

    public void SetSpeed(float value)
    {
        if(_speedText)
            _speedText.text = $"Speed: {value:0.00}";
    }

    public void OnPolarity(int sign)
    {
        ShowToast(sign > 0 ? "Polarity: +" : "Polarity: -", 0.6f);
    }

    public void ShowWinToast(float elapsedSeconds, bool isPersonalBest, int stars)
    {
        string starPart = stars > 0 ? $"  ★×{Mathf.Clamp(stars, 0, 3)}" : string.Empty;

        string msg = isPersonalBest
            ? $"PB! {Format(elapsedSeconds)}{starPart} — next…"
            : $"Level clear in {Format(elapsedSeconds)}{starPart} — next…";

        ShowToast(msg, 1.2f);
    }

    public void ShowFailToast(float elapsedSeconds)
    {
        string msg = $"Failed in {Format(elapsedSeconds)} — retry!";
        ShowToast(msg, _defaultToastSeconds);
    }

    public void ShowEngineRepairOffer(int powerPercent)
    {
        if(_engineRepairPanel == null)
            return;

        _engineRepairPanel.Show(powerPercent);
    }

    public void UpdateEngineDangerIndicator(int durability)
    {
        if(_dangerBtn == null || _dagerIcon == null)
            return;

        if(durability >= _dangerLowThreshold)
        {
            _dangerBtn.gameObject.SetActive(false);
            return;
        }

        _dangerBtn.gameObject.SetActive(true);

        if(durability <= _dangerHighThreshold)
            _dagerIcon.color = _highDangerColor;
        else
            _dagerIcon.color = _lowDangerColor;
    }

    // ---------- Event handlers ----------

    private void HandlePolarityChanged(int sign) => OnPolarity(sign);

    private void HandleSpeedChanged(float speed) => SetSpeed(speed);

    private void HandleStarCollected(int collected, Vector3 pos) => SetStars(collected);

    private void HandleTimeChanged(float timeSeconds) => SetTime(timeSeconds);

    // ---------- Внутреннее ----------

    private void OnDangerClicked()
    {
        int powerPercent = 100;
        if(ServiceLocator.TryGet<IProgressService>(out var progress))
            powerPercent = Mathf.RoundToInt(progress.EnginePower * 100f);

        if(_engineRepairPanel == null)
            return;

        if(ServiceLocator.TryGet<IModalService>(out var modal))
        {
            modal.Show(
                onShow: () => _engineRepairPanel.Show(powerPercent),
                onHide: null // панель сама прячет себя
            );
            return;
        }

        // fallback: если модал-сервиса нет
        _engineRepairPanel.Show(powerPercent);
    }

    private void ShowToast(string message, float seconds)
    {
        if(_toastText == null)
            return;

        _toastText.text = message;
        _toastText.gameObject.SetActive(true);

        if(_toastRoutine != null)
            StopCoroutine(_toastRoutine);

        _toastRoutine = StartCoroutine(CoHideToastAfter(seconds));
        ToastShown?.Invoke(message);
    }

    private IEnumerator CoHideToastAfter(float seconds)
    {
        float t = 0f;
        while(t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if(_toastText)
            _toastText.gameObject.SetActive(false);

        _toastRoutine = null;
    }

    private string Format(float totalSeconds)
    {
        if(!float.IsFinite(totalSeconds) || totalSeconds < 0f)
            return "0.00s";

        int minutes = Mathf.FloorToInt(totalSeconds / 60f);
        float secFloat = totalSeconds - minutes * 60f;

        int wholeSec = Mathf.FloorToInt(secFloat);
        int centisec = Mathf.FloorToInt((secFloat - wholeSec) * 100f);

        if(minutes > 0)
            return $"{minutes}:{wholeSec:00}.{centisec:00}s";

        return $"{wholeSec}.{centisec:00}s";
    }
}
