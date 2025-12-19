using System.Collections;
using UnityEngine;

public sealed class DefaultSkinView : PlayerSkinView
{
    [Header("Core & Trail")]
    [SerializeField] private SpriteRenderer _bodyInner;
    [SerializeField] private TrailRenderer _trail;

    [Header("Colors")]
    [SerializeField] private Color _plusColor = new(0.30f, 0.64f, 1f);
    [SerializeField] private Color _minusColor = new(1f, 0.42f, 0.42f);

    [Header("Engine SFX")]
    [SerializeField] private PlayerSfx _sfx;
    [SerializeField] private float _engineMinSpeed = 40f;
    [SerializeField] private float _engineMaxSpeed = 260f;
    [SerializeField] private float _engineResponse = 5f;

    [Header("Pulse Effect")]
    [SerializeField] private float _pulseDuration = 0.22f;
    [SerializeField] private float _pulseMin = 0.90f;
    [SerializeField] private float _pulseMax = 1.00f;

    private Coroutine _pulseRoutine;
    private float _engineLoadSmoothed;
    private Vector3 _originalScale;

    private void Awake()
    {
        if(_bodyInner != null)
            _originalScale = _bodyInner.transform.localScale;
        else
            _originalScale = Vector3.one;
    }

    // ----------------------------------------
    //  ПОЛЯРНОСТЬ
    // ----------------------------------------

    public override void OnPolarityChanged(int polarity)
    {
        // Цвет обновляем всегда
        UpdateColors(polarity);

        // Но эффекты/корутины только когда объект реально активен
        if(!isActiveAndEnabled)
            return;

        if(_sfx != null)
            _sfx.OnSwitchPolarity();

        if(_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private void UpdateColors(int polarity)
    {
        var color = polarity > 0 ? _plusColor : _minusColor;

        if(_bodyInner != null)
            _bodyInner.color = color;

        if(_trail != null)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );

            _trail.colorGradient = gradient;
        }
    }

    private IEnumerator PulseRoutine()
    {
        if(_bodyInner == null)
            yield break;

        float t = 0f;
        var baseScale = _originalScale;

        while(t < _pulseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / _pulseDuration);
            float s = Mathf.Lerp(_pulseMin, _pulseMax, k);

            _bodyInner.transform.localScale = baseScale * s;

            yield return null;
        }

        _bodyInner.transform.localScale = baseScale;
        _pulseRoutine = null;
    }

    // ----------------------------------------
    //  СКОРОСТЬ → ДВИГАТЕЛЬ
    // ----------------------------------------

    public override void OnSpeedChanged(float speed)
    {
        float targetLoad = CalcEngineLoad(speed);

        _engineLoadSmoothed = Mathf.MoveTowards(
            _engineLoadSmoothed,
            targetLoad,
            _engineResponse * Time.deltaTime
        );

        if(_sfx != null)
            _sfx.UpdateEngine(_engineLoadSmoothed, transform.position);
    }

    private float CalcEngineLoad(float speed)
    {
        if(_engineMaxSpeed <= _engineMinSpeed)
            return 0f;

        float t = Mathf.InverseLerp(_engineMinSpeed, _engineMaxSpeed, speed);
        t = Mathf.Clamp01(t);
        t *= t;

        return t;
    }

    // ----------------------------------------
    //  ВКЛ/ВЫКЛ
    // ----------------------------------------

    private void OnDisable()
    {
        if(_sfx != null)
            _sfx.StopEngine();

        if(_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        if(_bodyInner != null)
            _bodyInner.transform.localScale = _originalScale;
    }
}
