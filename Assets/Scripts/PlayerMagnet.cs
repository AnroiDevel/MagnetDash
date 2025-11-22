using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization; // для FormerlySerializedAs

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMagnet : MonoBehaviour
{
    [Header("Movement")]
    [FormerlySerializedAs("_startVX")]
    [SerializeField] private float _startUpSpeed = 120f;   // стартовая скорость по Y
    [SerializeField] private float _maxSpeed = 360f;
    [SerializeField] private float _cruiseMin = 60f;
    [SerializeField] private float _cruiseAccel = 120f;    // Ньютоновская сила (ForceMode2D.Force)

    [Header("Magnet")]
    [SerializeField] private float _k = 8000f;
    [SerializeField] private float _soft = 28f;
    [SerializeField] private float _maxForce = 800f;

    [Header("FX")]
    [SerializeField] private SpriteRenderer _bodyInner;
    [SerializeField] private TextMesh _symbol;             // можно заменить на TMP_Text для консистентности
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private Color _plusColor = new(0.30f, 0.64f, 1f);
    [SerializeField] private Color _minusColor = new(1f, 0.42f, 0.42f);

    [SerializeField] private PlayerSfx _playerSfx;

    public int Polarity { get; private set; } = +1;

    private Rigidbody2D _rb;

    private IInputService _input;
    private IUIService _ui;

    // локальные корутин-хэндлы, чтобы не убивать всё подряд
    private Coroutine _pulseRoutine;
    private Coroutine _absorbRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        // Для Unity 6 у тебя используется linearVelocity — оставляю как есть
        _rb.linearVelocity = new Vector2(0f, _startUpSpeed);
        UpdateVisual();
    }

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.TogglePolarity += OnToggle;
        });
        ServiceLocator.WhenAvailable<IUIService>(ui => _ui = ui);
    }

    private void OnDisable()
    {
        if(_input != null)
            _input.TogglePolarity -= OnToggle;
    }

    private void OnToggle()
    {
        // Реагируем на инпут только в активном геймплее
        if(ServiceLocator.TryGet<LevelManager>(out var lm) &&
           lm.State != GameState.Playing)
            return;

        TogglePolarity();
    }

    private void FixedUpdate()
    {
        Vector2 totalF = Vector2.zero;
        List<MagneticNode> nodes = MagneticNode.All;
        Vector2 p = _rb.position;

        for(int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            Vector2 d = n.Position - p;
            float dist = d.magnitude;
            if(dist < 1e-3f)
                continue;

            float falloff = 1f / (dist + _soft);
            int sign = Polarity * n.Charge; // +1 притяжение, -1 отталкивание (или наоборот — зависит от физики)
            float f = Mathf.Clamp(_k * sign * falloff, -_maxForce, _maxForce);
            Vector2 dir = d / dist;
            totalF += dir * f;
        }

        // ВАЖНО: при ForceMode2D.Force НЕ умножаем на fixedDeltaTime
        _rb.AddForce(totalF, ForceMode2D.Force);

        // Экспоненциальный «ручной» воздушный тормоз (DT-инвариант)
        const float dragPerSec = 0.995f;
        float drag = Mathf.Pow(dragPerSec, Time.fixedDeltaTime * 60f);
        _rb.linearVelocity *= drag;

        // Крейз-контроль: подталкиваем по направлению движения (или вверх, если почти стоим)
        float sp = _rb.linearVelocity.magnitude;
        if(sp < _cruiseMin)
        {
            Vector2 dir = sp > 0.01f ? (_rb.linearVelocity / sp) : Vector2.up;
            _rb.AddForce(dir * _cruiseAccel, ForceMode2D.Force);
            sp = _rb.linearVelocity.magnitude;
        }

        if(sp > _maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _maxSpeed;

        _ui?.SetSpeed(sp);
    }


    public void TogglePolarity()
    {
        Polarity *= -1;
        UpdateVisual();

        if(_playerSfx)
            _playerSfx.OnSwitchPolarity();

        if(_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseInner());

        _ui?.OnPolarity(Polarity);

        if(ServiceLocator.TryGet<IGameEvents>(out var ev))
            ev.FirePolaritySwitched();
    }


    private void UpdateVisual()
    {
        var c = (Polarity > 0) ? _plusColor : _minusColor;

        if(_bodyInner)
            _bodyInner.color = c;

        if(_symbol)
        {
            _symbol.text = (Polarity > 0) ? "+" : "−";
            _symbol.color = Color.white;
        }

        if(_trail)
        {
            var g = new Gradient();
            var col = c;
            g.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 1f) },
                new[] { new GradientAlphaKey(col.a, 0f), new GradientAlphaKey(0f, 1f) }
            );
            _trail.colorGradient = g;
        }
    }

    private IEnumerator PulseInner()
    {
        float t = 0f;
        const float dur = 0.12f;
        const float min = 0.90f, max = 1.00f;
        while(t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            float s = Mathf.Lerp(min, max, k);
            if(_bodyInner)
                _bodyInner.transform.localScale = new Vector3(0.82f * s, 0.82f * s, 1f);
            yield return null;
        }
        _pulseRoutine = null;
    }

    public void AbsorbIntoPortal(Vector3 portalPosition, float duration = 1.2f)
    {
        if(_absorbRoutine != null)
            return;

        // Сразу фиксируем победу, чтобы запретить паузу/смерть/повторный триггер
        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
        {
            flow.CompleteLevel();
        }
        else
        {
            Debug.LogError("[PlayerMagnet] ILevelFlow service not found. " +
                           "Ensure LevelManager is present in the Systems scene and registered.");
        }

        _absorbRoutine = StartCoroutine(AbsorbRoutine(portalPosition, duration));
    }

    private IEnumerator AbsorbRoutine(Vector3 portalPosition, float duration)
    {
        // Отключаем управление и физику
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        const float spinSpeed = 360f; // град/сек, стабильное вращение

        float t = 0f;
        while(t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = 1f - Mathf.Pow(1f - k, 2f); // ease-in

            // Тянем к центру портала
            transform.position = Vector3.Lerp(startPos, portalPosition, ease);

            // Ровное вращение вокруг портала
            transform.RotateAround(portalPosition, Vector3.forward, -spinSpeed * Time.deltaTime);

            // Сжимаем к нулю
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);

            yield return null;
        }

        // Фейд-аут спрайта (если есть)
        var sr = GetComponentInChildren<SpriteRenderer>();
        if(sr)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);


        _absorbRoutine = null;

        gameObject.SetActive(false);
    }
}
