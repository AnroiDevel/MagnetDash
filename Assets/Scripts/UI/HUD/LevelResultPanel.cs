using System.Collections;
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
    [SerializeField] private Image[] _starImages;   // 3 Image под звезды (слева-направо)
    [SerializeField] private Sprite _starActive;    // яркая звезда
    [SerializeField] private Sprite _starDim;       // силуэт/тусклая

    [Header("Buttons")]
    [SerializeField] private Button _btnRetry;
    [SerializeField] private Button _btnNext;
    [SerializeField] private Button _btnMenu;

    [Header("FX")]
    [SerializeField] private float _fadeDuration = 0.25f;

    [Header("Final panel")]
    [SerializeField] private GameObject _finalPanel; // <-- НОВОЕ: панель финала (изначально выключена)

    private IGameFlowEvents _flowEvents;
    private ILevelFlow _flow;
    private Coroutine _fadeRoutine;

    // --- новое ---
    private LevelResultInfo _currentInfo;
    private bool _isFinalLevel;

    private void Awake()
    {
        if(_cg == null)
            _cg = GetComponentInChildren<CanvasGroup>(true);

        HideImmediate();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IGameFlowEvents>(e =>
        {
            if(!isActiveAndEnabled)
                return;

            _flowEvents = e;
            _flowEvents.LevelCompleted += OnLevelCompleted;
            _flowEvents.LevelFailed += OnLevelFailed;
        });

        ServiceLocator.WhenAvailable<ILevelFlow>(f =>
        {
            _flow = f;
        });

        _btnRetry.onClick.AddListener(OnRetryClicked);
        _btnNext.onClick.AddListener(OnNextClicked);
        _btnMenu.onClick.AddListener(OnMenuClicked);
    }

    private void OnDisable()
    {
        if(_flowEvents != null)
        {
            _flowEvents.LevelCompleted -= OnLevelCompleted;
            _flowEvents.LevelFailed -= OnLevelFailed;
            _flowEvents = null;
        }

        _btnRetry.onClick.RemoveListener(OnRetryClicked);
        _btnNext.onClick.RemoveListener(OnNextClicked);
        _btnMenu.onClick.RemoveListener(OnMenuClicked);

        if(_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _currentInfo = null;
        _isFinalLevel = false;

        HideImmediate();
    }

    private void OnLevelCompleted(LevelResultInfo info)
    {
        Show(info);
    }

    private void OnLevelFailed(LevelResultInfo info)
    {
        Show(info);
    }

    private void Show(LevelResultInfo info)
    {
        if(info == null)
            return;

        _currentInfo = info;
        _isFinalLevel = info.isWin && info.levelNumber == 12;

        _tTitle.SetText(info.levelNumber.ToString());
        _tTime.SetText("Время: {0:0.00} c", info.elapsedTime);

        if(info.bestTime.HasValue)
        {
            if(info.isPersonalBest)
                _tBest.SetText("Лучшее: {0:0.00} c (новый рекорд!)", info.bestTime.Value);
            else
                _tBest.SetText("Лучшее: {0:0.00} c", info.bestTime.Value);
        }
        else
        {
            _tBest.SetText("Лучшее: —");
        }

        UpdateStars(info.collectedStars);

        _tHint.SetText(string.IsNullOrEmpty(info.hint) ? string.Empty : info.hint);

        // На экране результата «Далее» всегда видно
        _btnNext.gameObject.SetActive(true);

        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeIn());
    }

    private void UpdateStars(int collected)
    {
        int max = _starImages.Length;
        int clamped = Mathf.Clamp(collected, 0, max);

        for(int i = 0; i < max; i++)
        {
            var img = _starImages[i];
            if(img == null)
                continue;

            bool isOn = i < clamped;
            img.sprite = isOn ? _starActive : _starDim;
            img.enabled = true;
        }
    }

    private void OnRetryClicked()
    {
        if(_flow != null)
            _flow.Reload();
        Hide();
    }

    private void OnNextClicked()
    {
        // Если это финал (победа на 12 уровне) — вместо LoadNext показываем финальную панель
        //if(_isFinalLevel && _currentInfo != null)
        //{
        //    if(_finalPanel != null)
        //        _finalPanel.SetActive(true);

        //    // Скрываем панель результата
        //    Hide();
        //    return;
        //}

        // Обычное поведение
        if(_flow != null)
            _flow.LoadNext();
        Hide();
    }

    private void OnMenuClicked()
    {
        if(_flow != null)
            _flow.LoadMenu();
        Hide();
    }

    public void Hide()
    {
        if(_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOut());
    }

    private void HideImmediate()
    {
        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
    }

    private IEnumerator FadeIn()
    {
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
