using System.Collections;
using UnityEngine;

public sealed class LogoPolarityFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _plus;
    [SerializeField] private RectTransform _minus;

    [Header("Breathing")]
    [SerializeField] private float _breathAmplitude = 0.03f;   // 3% от базового масштаба
    [SerializeField] private float _breathSpeed = 1.0f;        // скорость дыхания
    [SerializeField] private float _breathPhaseOffset = 1.2f;  // сдвиг фазы минуса

    [Header("Magnetic Attraction")]
    [SerializeField] private float _attractDistance = 4f;      // пиксели к центру
    [SerializeField] private float _attractDuration = 0.18f;   // туда
    [SerializeField] private float _returnDuration = 0.22f;    // обратно
    [SerializeField] private float _intervalMin = 3f;
    [SerializeField] private float _intervalMax = 6f;

    private RectTransform _plusRect => _plus;
    private RectTransform _minusRect => _minus;

    private Vector3 _plusBasePos;
    private Vector3 _minusBasePos;
    private Vector3 _plusBaseScale;
    private Vector3 _minusBaseScale;

    private Vector3 _plusOffset;
    private Vector3 _minusOffset;

    private float _time;
    private Coroutine _attractRoutine;
    private float _nextAttractTime;

    private void Awake()
    {
        if(_plus != null)
        {
            _plusBasePos = _plus.localPosition;
            _plusBaseScale = _plus.localScale;
        }

        if(_minus != null)
        {
            _minusBasePos = _minus.localPosition;
            _minusBaseScale = _minus.localScale;
        }

        ScheduleNextAttraction();
    }

    private void OnEnable()
    {
        _time = 0f;
        _plusOffset = Vector3.zero;
        _minusOffset = Vector3.zero;
        ScheduleNextAttraction();
    }

    private void Update()
    {
        if(_plus == null || _minus == null)
            return;

        float dt = Time.unscaledDeltaTime;
        _time += dt;

        UpdateBreathing();
        UpdateAttractionTrigger();
    }

    private void UpdateBreathing()
    {
        // дыхание плюса
        float sPlus = 1f + Mathf.Sin(_time * _breathSpeed) * _breathAmplitude;
        // дыхание минуса со сдвигом фазы
        float sMinus = 1f + Mathf.Sin(_time * _breathSpeed + _breathPhaseOffset) * _breathAmplitude;

        _plus.localScale = _plusBaseScale * sPlus;
        _minus.localScale = _minusBaseScale * sMinus;

        // позиция: базовая + магнитное смещение
        _plus.localPosition = _plusBasePos + _plusOffset;
        _minus.localPosition = _minusBasePos + _minusOffset;
    }

    private void UpdateAttractionTrigger()
    {
        if(_attractRoutine != null)
            return;

        if(Time.unscaledTime >= _nextAttractTime)
        {
            _attractRoutine = StartCoroutine(AttractionRoutine());
            ScheduleNextAttraction();
        }
    }

    private void ScheduleNextAttraction()
    {
        float interval = Random.Range(_intervalMin, _intervalMax);
        _nextAttractTime = Time.unscaledTime + interval;
    }

    private IEnumerator AttractionRoutine()
    {
        // направление к центру между плюс и минус
        Vector3 center = (_plusBasePos + _minusBasePos) * 0.5f;
        Vector3 plusDir = (center - _plusBasePos).normalized;
        Vector3 minusDir = (center - _minusBasePos).normalized;

        Vector3 plusTarget = plusDir * _attractDistance;
        Vector3 minusTarget = minusDir * _attractDistance;

        // движение к центру
        float t = 0f;
        float dur = Mathf.Max(0.01f, _attractDuration);
        while(t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = k * k * (3f - 2f * k); // smoothstep

            _plusOffset = Vector3.Lerp(Vector3.zero, plusTarget, k);
            _minusOffset = Vector3.Lerp(Vector3.zero, minusTarget, k);
            yield return null;
        }

        // возврат обратно
        t = 0f;
        dur = Mathf.Max(0.01f, _returnDuration);
        while(t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = k * k * (3f - 2f * k);

            _plusOffset = Vector3.Lerp(plusTarget, Vector3.zero, k);
            _minusOffset = Vector3.Lerp(minusTarget, Vector3.zero, k);
            yield return null;
        }

        _plusOffset = Vector3.zero;
        _minusOffset = Vector3.zero;
        _attractRoutine = null;
    }
}
