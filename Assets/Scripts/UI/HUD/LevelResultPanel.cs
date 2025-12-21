using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelResultPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private TMP_Text _tTitle;
    [SerializeField] private TMP_Text _tTime;
    [SerializeField] private TMP_Text _tBest;
    [SerializeField] private TMP_Text _tHint;

    [Header("Stars")]
    [SerializeField] private Image[] _starImages;
    [SerializeField] private Sprite _starActive;
    [SerializeField] private Sprite _starDim;

    [Header("Reward")]
    [SerializeField] private GameObject _rewardRoot;
    [SerializeField] private TMP_Text _tRewardTotal;
    [SerializeField] private TMP_Text _tRewardLines;

    [Header("Ads")]
    [SerializeField] private Button _btnDoubleReward;
    [SerializeField] private TMP_Text _tDoubleRewardLabel;

    // В вебе прелоад может быть “длинным”, 2 секунды часто мало.
    [SerializeField, Min(0.1f)] private float _rewardedWaitSeconds = 15.0f;

    [Header("Buttons")]
    [SerializeField] private Button _btnRetry;
    [SerializeField] private Button _btnNext;
    [SerializeField] private Button _btnMenu;

    [Header("FX")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;

    private IGameFlowEvents _flowEvents;
    private ILevelFlow _flow;

    private Coroutine _fadeRoutine;
    private Coroutine _waitRewardedRoutine;

    private LevelResultInfo _currentInfo;
    private bool _adInFlight;

    private void Awake()
    {
        if(_cg == null)
            _cg = GetComponentInChildren<CanvasGroup>(true);

        HideImmediate();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IGameFlowEvents>(BindFlowEvents);
        ServiceLocator.WhenAvailable<ILevelFlow>(BindLevelFlow);

        if(_btnRetry != null)
            _btnRetry.onClick.AddListener(OnRetryClicked);
        if(_btnNext != null)
            _btnNext.onClick.AddListener(OnNextClicked);
        if(_btnMenu != null)
            _btnMenu.onClick.AddListener(OnMenuClicked);
        if(_btnDoubleReward != null)
            _btnDoubleReward.onClick.AddListener(OnDoubleRewardClicked);
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<IGameFlowEvents>(BindFlowEvents);
        ServiceLocator.Unsubscribe<ILevelFlow>(BindLevelFlow);

        UnbindFlowEvents();
        _flow = null;

        if(_btnRetry != null)
            _btnRetry.onClick.RemoveListener(OnRetryClicked);
        if(_btnNext != null)
            _btnNext.onClick.RemoveListener(OnNextClicked);
        if(_btnMenu != null)
            _btnMenu.onClick.RemoveListener(OnMenuClicked);
        if(_btnDoubleReward != null)
            _btnDoubleReward.onClick.RemoveListener(OnDoubleRewardClicked);

        StopRoutines();

        _currentInfo = null;
        _adInFlight = false;
        HideImmediate();
    }

    private void StopRoutines()
    {
        if(_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if(_waitRewardedRoutine != null)
        {
            StopCoroutine(_waitRewardedRoutine);
            _waitRewardedRoutine = null;
        }
    }

    private void BindFlowEvents(IGameFlowEvents e)
    {
        if(!isActiveAndEnabled)
            return;

        if(ReferenceEquals(_flowEvents, e))
            return;

        UnbindFlowEvents();
        _flowEvents = e;

        if(_flowEvents == null)
            return;

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

    private void BindLevelFlow(ILevelFlow f)
    {
        if(!isActiveAndEnabled)
            return;

        _flow = f;
    }

    private void OnLevelCompleted(LevelResultInfo info) => Show(info);
    private void OnLevelFailed(LevelResultInfo info) => Show(info);

    private void Show(LevelResultInfo info)
    {
        if(info == null)
            return;

        if(_waitRewardedRoutine != null)
        {
            StopCoroutine(_waitRewardedRoutine);
            _waitRewardedRoutine = null;
        }

        _currentInfo = info;
        _adInFlight = false;

        if(_tTitle != null)
            _tTitle.SetText(info.levelNumber.ToString());
        if(_tTime != null)
            _tTime.SetText("Время: {0:0.00} c", info.elapsedTime);

        if(_tBest != null)
        {
            if(info.bestTime.HasValue)
            {
                _tBest.SetText(
                    info.isPersonalBest ? "Лучшее: {0:0.00} c (новый рекорд!)" : "Лучшее: {0:0.00} c",
                    info.bestTime.Value);
            }
            else
                _tBest.SetText("Лучшее: —");
        }

        UpdateStars(info.collectedStars);

        if(_tHint != null)
            _tHint.SetText(string.IsNullOrEmpty(info.hint) ? string.Empty : info.hint);

        if(_btnNext != null)
            _btnNext.gameObject.SetActive(info.isWin);

        UpdateRewardUi(info);

        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeIn());
    }

    private void UpdateRewardUi(LevelResultInfo info)
    {
        var r = info.reward;
        bool showReward = info.isWin && r != null && r.baseReward > 0;

        if(_rewardRoot != null)
            _rewardRoot.SetActive(showReward);

        if(!showReward)
        {
            SetDoubleButtonVisible(false);
            return;
        }

        if(_tRewardTotal != null)
            _tRewardTotal.SetText("НАГРАДА   +{0}", r.Total);

        if(_tRewardLines != null)
            _tRewardLines.SetText(BuildRewardLines(r, info.collectedStars));

        bool available = false;
        bool ready = false;

        if(ServiceLocator.TryGet<IAdService>(out var ads) && ads != null)
        {
            available = ads.IsAvailable;
            ready = ads.IsAvailable && ads.IsRewardedReady;

            // Если всё ок, но rewarded ещё не готов — прелоадим и ждём дольше (unscaled)
            if(available && r.CanDoubleNow && !_adInFlight && !ready)
            {
                ads.PreloadRewarded();

                if(_waitRewardedRoutine != null)
                    StopCoroutine(_waitRewardedRoutine);

                _waitRewardedRoutine = StartCoroutine(CoWaitRewardedReadyThenRefresh());
            }
        }

        bool showDoubleButton = available && ready && r.CanDoubleNow && !_adInFlight;

        SetDoubleButtonVisible(showDoubleButton);

        if(_tDoubleRewardLabel != null)
            _tDoubleRewardLabel.SetText("🎥 Удвоить (+{0})", r.baseReward);
    }

    private void SetDoubleButtonVisible(bool visible)
    {
        if(_btnDoubleReward == null)
            return;

        _btnDoubleReward.gameObject.SetActive(visible);
        _btnDoubleReward.interactable = visible;
    }

    private IEnumerator CoWaitRewardedReadyThenRefresh()
    {
        float t = 0f;

        while(t < _rewardedWaitSeconds)
        {
            if(!isActiveAndEnabled)
                yield break;

            if(ServiceLocator.TryGet<IAdService>(out var ads) && ads != null && ads.IsAvailable && ads.IsRewardedReady)
                break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _waitRewardedRoutine = null;

        if(isActiveAndEnabled && _currentInfo != null)
            UpdateRewardUi(_currentInfo);
    }

    private static string BuildRewardLines(LevelRewardInfo r, int stars)
    {
        var sb = new StringBuilder(64);

        if(r.firstClear > 0)
            sb.Append('+').Append(r.firstClear).Append("  Прохождение\n");
        if(r.starsDelta > 0)
            sb.Append('+').Append(r.starsDelta).Append("  Звёзды (+").Append(stars).Append(")\n");
        if(r.timeRecord > 0)
            sb.Append('+').Append(r.timeRecord).Append("  Рекорд\n");
        if(r.doubleBonus > 0)
            sb.Append('+').Append(r.doubleBonus).Append("  Удвоение\n");

        if(sb.Length == 0)
            return string.Empty;

        sb.Length -= 1;
        return sb.ToString();
    }

    private void UpdateStars(int collected)
    {
        if(_starImages == null)
            return;

        int max = _starImages.Length;
        int clamped = Mathf.Clamp(collected, 0, max);

        for(int i = 0; i < max; i++)
        {
            var img = _starImages[i];
            if(img == null)
                continue;

            img.sprite = (i < clamped) ? _starActive : _starDim;
            img.enabled = true;
        }
    }

    private void OnRetryClicked()
    {
        _flow?.Reload();
        Hide();
    }

    private void OnNextClicked()
    {
        _flow?.LoadNext();
        Hide();
    }

    private void OnMenuClicked()
    {
        _flow?.LoadMenu();
        Hide();
    }

    private void OnDoubleRewardClicked()
    {
        if(_adInFlight)
            return;

        var info = _currentInfo;
        var r = info?.reward;

        if(info == null || r == null || !info.isWin || !r.CanDoubleNow)
            return;

        // Для запуска требуем доступность сервиса. Готовность rewarded — желательно,
        // но реальную "pending очередь" лучше держать внутри VkAdService.
        if(!ServiceLocator.TryGet<IAdService>(out var ads) || ads == null || !ads.IsAvailable)
            return;

        _adInFlight = true;

        // ВАЖНО: сразу обновляем UI, чтобы кнопка исчезла (а не просто стала disabled).
        UpdateRewardUi(info);

        ads.ShowRewarded(
            onSuccess: () =>
            {
                _adInFlight = false;

                if(ServiceLocator.TryGet<ICurrencyService>(out var currency))
                    currency.Add(r.baseReward);

                r.doubled = true;
                r.doubleBonus += r.baseReward;

                UpdateRewardUi(info);
            },
            onFail: () =>
            {
                _adInFlight = false;
                UpdateRewardUi(info);
            }
        );
    }

    public void Hide()
    {
        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeOut());
    }

    private void HideImmediate()
    {
        if(_cg == null)
            return;

        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }

    private IEnumerator FadeIn()
    {
        if(_cg == null)
            yield break;

        _cg.blocksRaycasts = true;
        _cg.interactable = true;

        float t = 0f;
        while(t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.SmoothStep(0f, 1f, t / _fadeDuration);
            yield return null;
        }

        _cg.alpha = 1f;
        _fadeRoutine = null;
    }

    private IEnumerator FadeOut()
    {
        if(_cg == null)
            yield break;

        float t = 0f;
        while(t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.SmoothStep(1f, 0f, t / _fadeDuration);
            yield return null;
        }

        HideImmediate();
        _fadeRoutine = null;
    }
}
