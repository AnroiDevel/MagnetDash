using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelResultPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup _group;

    [Header("Text templates")]
    [SerializeField] private string _titleWin = "ÓÐÎÂÅÍÜ ÏÐÎÉÄÅÍ";
    [SerializeField] private string _levelFmt = "Óðîâåíü {0}";
    [SerializeField] private string _timeFmt = "Âðåìÿ: {0}";
    [SerializeField] private string _bestFmt = "Ðåêîðä: {0}";
    [SerializeField] private string _pbLabel = "PB";

    [Header("Texts")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _bestText;
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private TMP_Text _pbBadgeText;

    [Header("Stars")]
    [SerializeField] private Image[] _starImages;
    [SerializeField] private Color _starOnColor = Color.yellow;
    [SerializeField] private Color _starOffColor = new(1f, 1f, 1f, 0.25f);

    [Header("Star animation")]
    [SerializeField, Min(0f)] private float _starAppearDelay = 0.18f;
    [SerializeField, Min(0f)] private float _starScaleDuration = 0.12f;
    [SerializeField, Min(1f)] private float _starScaleFrom = 1.4f;

    [Header("Buttons")]
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _menuButton;

    public event Action RetryRequested;
    public event Action NextRequested;
    public event Action MenuRequested;

    private Coroutine _starsRoutine;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<LevelManager>(lm => lm.RegisterResultPanel(this));
    }

    private void OnDisable()
    {
        if(ServiceLocator.TryGet<LevelManager>(out var lm))
            lm.UnregisterResultPanel(this);
    }

    private void Awake()
    {
        if(_group != null)
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        if(_pbBadgeText != null)
        {
            _pbBadgeText.text = _pbLabel;
            _pbBadgeText.gameObject.SetActive(false);
        }

        AttachButton(_retryButton, () => RetryRequested?.Invoke());
        AttachButton(_nextButton, () => NextRequested?.Invoke());
        AttachButton(_menuButton, () => MenuRequested?.Invoke());
    }

    private void OnDestroy()
    {
        DetachButton(_retryButton);
        DetachButton(_nextButton);
        DetachButton(_menuButton);
    }

    private void AttachButton(Button button, Action handler)
    {
        if(button == null || handler == null)
            return;
        button.onClick.AddListener(() => handler());
    }

    private void DetachButton(Button button)
    {
        if(button == null)
            return;
        button.onClick.RemoveAllListeners();
    }

    public void ShowWin(
        int levelNumber,
        float timeSeconds,
        float? bestSeconds,
        int starsCollected,
        bool isPb,
        string hintText)
    {
        if(_group == null)
            return;

        if(_titleText != null)
            _titleText.text = _titleWin;

        if(_levelText != null)
            _levelText.text = string.Format(_levelFmt, levelNumber);

        if(_timeText != null)
            _timeText.text = string.Format(_timeFmt, FormatTime(timeSeconds));

        if(_bestText != null)
        {
            if(bestSeconds.HasValue)
            {
                _bestText.gameObject.SetActive(true);
                _bestText.text = string.Format(_bestFmt, FormatTime(bestSeconds.Value));
            }
            else
            {
                _bestText.gameObject.SetActive(false);
            }
        }

        if(_pbBadgeText != null)
            _pbBadgeText.gameObject.SetActive(isPb);

        if(_hintText != null)
            _hintText.text = hintText ?? string.Empty;

        UpdateStarsInstant(0);

        if(_starsRoutine != null)
        {
            StopCoroutine(_starsRoutine);
            _starsRoutine = null;
        }
        _starsRoutine = StartCoroutine(AnimateStars(starsCollected));

        _group.alpha = 1f;
        _group.interactable = true;
        _group.blocksRaycasts = true;
    }

    public void Hide()
    {
        if(_group == null)
            return;

        if(_starsRoutine != null)
        {
            StopCoroutine(_starsRoutine);
            _starsRoutine = null;
        }

        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }

    private IEnumerator AnimateStars(int starsCollected)
    {
        if(_starImages == null || _starImages.Length == 0)
            yield break;

        UpdateStarsInstant(0);

        int max = Mathf.Clamp(starsCollected, 0, _starImages.Length);

        for(int i = 0; i < max; i++)
        {
            var img = _starImages[i];
            if(img == null)
                continue;

            img.color = _starOnColor;

            Transform tr = img.transform;
            Vector3 baseScale = Vector3.one;
            float t = 0f;

            while(t < _starScaleDuration)
            {
                t += Time.deltaTime;
                float k = (_starScaleDuration > 0f) ? t / _starScaleDuration : 1f;
                float s = Mathf.Lerp(_starScaleFrom, 1f, k);
                tr.localScale = baseScale * s;
                yield return null;
            }

            tr.localScale = baseScale;

            if(_starAppearDelay > 0f)
                yield return new WaitForSeconds(_starAppearDelay);
        }

        _starsRoutine = null;
    }

    private void UpdateStarsInstant(int countOn)
    {
        if(_starImages == null || _starImages.Length == 0)
            return;

        int len = _starImages.Length;
        int on = Mathf.Clamp(countOn, 0, len);

        for(int i = 0; i < len; i++)
        {
            var img = _starImages[i];
            if(img == null)
                continue;

            img.color = (i < on) ? _starOnColor : _starOffColor;
            img.transform.localScale = Vector3.one;
        }
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int cs = Mathf.FloorToInt((seconds - totalSeconds) * 100f);

        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;

        if(h > 0)
            return $"{h}:{m:00}:{s:00}.{cs:00}";

        if(m > 0)
            return $"{m}:{s:00}.{cs:00}";

        return $"{s:00}.{cs:00}";
    }
}
