using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EngineRepairPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _power;
    [SerializeField] private Button _repairButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Slider _powerSlider;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float _fadeInSeconds = 0.15f;
    [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.12f;

    private CanvasGroup _canvasGroup;
    private IModalService _modal;

    private bool _waitingAd;
    private bool _isTransitioning;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if(_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _repairButton.onClick.AddListener(OnRepairClicked);
        _continueButton.onClick.AddListener(OnContinueClicked);

        ServiceLocator.WhenAvailable<IModalService>(m => _modal = m);

        // консистентный старт
        if(gameObject.activeSelf)
            SetVisibleInstant(false);
    }

    private void OnDestroy()
    {
        _repairButton.onClick.RemoveListener(OnRepairClicked);
        _continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    public void Show(int powerPercent)
    {
        _waitingAd = false;

        if(_power != null)
            _power.text = $"{powerPercent}%";

        if(_powerSlider != null)
            _powerSlider.value = powerPercent;

        SetButtons(waitingAd: false);

        gameObject.SetActive(true);

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;

        StartFade(toAlpha: 1f, seconds: _fadeInSeconds);

        if(ServiceLocator.TryGet<IAdService>(out var ad) && ad is VkAdService vkAd)
            vkAd.PreloadRewarded();
    }

    private void OnRepairClicked()
    {
        if(_waitingAd || _isTransitioning)
            return;

        ServiceLocator.TryGet<IProgressService>(out var progress);
        ServiceLocator.TryGet<IAdService>(out var ad);

        void RepairNow()
        {
            progress?.RepairEngineFull();
            Close();
        }

        if(ad == null || !ad.IsAvailable)
        {
            RepairNow();
            return;
        }

        _waitingAd = true;
        SetButtons(waitingAd: true);

        ad.ShowRewarded(
            onSuccess: RepairNow,
            onFail: Close
        );
    }

    private void OnContinueClicked()
    {
        if(_waitingAd || _isTransitioning)
            return;

        Close();
    }

    private void SetButtons(bool waitingAd)
    {
        if(_continueButton != null)
            _continueButton.interactable = !waitingAd;

        if(_repairButton != null)
            _repairButton.interactable = !waitingAd;
    }

    private void Close()
    {
        if(_isTransitioning)
            return;

        _waitingAd = false;
        SetButtons(waitingAd: false);

        _canvasGroup.interactable = false;

        StartFade(toAlpha: 0f, seconds: _fadeOutSeconds, onComplete: () =>
        {
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            _modal?.Close();
        });
    }

    private void StartFade(float toAlpha, float seconds, Action onComplete = null)
    {
        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeRoutine(toAlpha, seconds, onComplete));
    }

    private IEnumerator FadeRoutine(float toAlpha, float seconds, Action onComplete)
    {
        _isTransitioning = true;

        float from = _canvasGroup.alpha;
        float t = 0f;

        if(seconds <= 0.0001f)
        {
            _canvasGroup.alpha = toAlpha;
            _isTransitioning = false;
            onComplete?.Invoke();
            yield break;
        }

        while(t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            float ease = 1f - Mathf.Pow(1f - k, 2f);

            _canvasGroup.alpha = Mathf.Lerp(from, toAlpha, ease);
            yield return null;
        }

        _canvasGroup.alpha = toAlpha;

        _isTransitioning = false;
        _fadeRoutine = null;

        onComplete?.Invoke();
    }

    private void SetVisibleInstant(bool visible)
    {
        if(_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _isTransitioning = false;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;
        gameObject.SetActive(visible);
    }
}
