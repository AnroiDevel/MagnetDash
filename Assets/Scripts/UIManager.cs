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
    [SerializeField] private Color _emptyColor = new Color(1, 1, 1, 0.25f);

    [Header("Toast")]
    [SerializeField] private float _defaultToastSeconds = 1.0f;

    private Coroutine _toastRoutine;

    public event System.Action<string> ToastShown;

    private void Awake()
    {
        ServiceLocator.Register<IUIService>(this);

        if(_toastText)
            _toastText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IUIService>(this);
    }

    // ---------- IUIService ----------

    public void SetStars(int collected, int total)
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
            _starCountText.text = $"{collected} / {total}";
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

        if(progress != null && progress.TryGetBestTime(progressKey, out float best))
        {
            has = float.IsFinite(best);
        }
        else
        {
            best = 0f; // значение не важно, мы его не используем, если has == false
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
        // elapsedSeconds можно использовать в тексте, если нужно
        string msg = $"Failed in {Format(elapsedSeconds)} — retry!";
        ShowToast(msg, _defaultToastSeconds);
    }

    // ---------- Внутреннее ----------

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
