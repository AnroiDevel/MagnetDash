using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour, IUIService
{
    [Header("Texts")]
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _bestText;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _toastText;

    [Header("Toast")]
    [SerializeField] private float _defaultToastSeconds = 1.0f;

    private Coroutine _toastRoutine;

    public event System.Action<string> ToastShown;

    private void Awake()
    {
        // Регистрируемся как сервис UI
        ServiceLocator.Register<IUIService>(this);
        // Спрячем тост при старте
        if(_toastText)
            _toastText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Чистим сервис при выгрузке сцены
        ServiceLocator.Unregister<IUIService>(this);
    }

    // ---------- Публичный API (IUIService) ----------

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

    public void RefreshBest(float? bestSeconds)
    {
        if(!_bestText)
            return;

        bool has = bestSeconds.HasValue;
        _bestText.gameObject.SetActive(has);
        if(has)
            _bestText.text = $"Best: {Format(bestSeconds.Value)}";
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
        // Используем stars в сообщении (если >0)
        string starPart = stars > 0 ? $"  ★×{Mathf.Clamp(stars, 0, 3)}" : string.Empty;
        string msg = isPersonalBest
            ? $"PB! {Format(elapsedSeconds)}{starPart} — next…"
            : $"Level clear in {Format(elapsedSeconds)}{starPart} — next…";

        ShowToast(msg, 1.2f);
    }

    public void ShowFailToast()
    {
        ShowToast("Ouch! Press R to retry.", _defaultToastSeconds);
    }

    // ---------- Внутреннее ----------

    private void ShowToast(string message, float seconds)
    {
        if(_toastText == null)
            return;

        _toastText.text = message;
        _toastText.gameObject.SetActive(true);

        // Отменяем предыдущую корутину, если была
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
            t += Time.unscaledDeltaTime; // тост скрывается даже на паузе
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
        else
            return $"{wholeSec}.{centisec:00}s";
    }

    public void RefreshBest(float? bestSeconds, IProgressService progress)
    {
        if(!_bestText)
            return;

        bool has = bestSeconds.HasValue;
        _bestText.gameObject.SetActive(has);
        if(has)
            _bestText.text = $"Best: {Format(bestSeconds.Value)}";
    }

    public void ShowFailToast(float elapsed)
    {
        throw new System.NotImplementedException();
    }
}
