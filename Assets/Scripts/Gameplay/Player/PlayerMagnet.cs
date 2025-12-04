using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMagnet : MonoBehaviour
{
    #region Serialized Fields

    [Header("Config")]
    [SerializeField] private PlayerMagnetConfig _config;

    [Header("Scene References")]
    [SerializeField] private Transform _portalTarget;
    [SerializeField] private SpriteRenderer _bodyInner;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private PlayerSfx _playerSfx;

    #endregion

    #region Config Accessors (safe defaults)

    private float StartUpSpeed => _config != null ? _config.startUpSpeed : 120f;
    private float MaxSpeed => _config != null ? _config.maxSpeed : 360f;
    private float CruiseMin => _config != null ? _config.cruiseMin : 60f;
    private float CruiseAccel => _config != null ? _config.cruiseAccel : 120f;

    private float TurnSpeedDeg => _config != null ? _config.turnSpeedDeg : 360f;
    private float MinSpeedForRotate => _config != null ? _config.minSpeedForRotate : 5f;

    private float PortalMagnetForce => _config != null ? _config.portalMagnetForce : 5f;
    private float PortalMagnetMaxDistance => _config != null ? _config.portalMagnetMaxDistance : 100f;
    private float PortalMagnetStopDistance => _config != null ? _config.portalMagnetStopDistance : 0.5f;

    private float OrbitRadiusUnits => _config != null ? _config.orbitRadiusUnits : 2.5f;
    private float OrbitCaptureFactor => _config != null ? _config.orbitCaptureFactor : 0.7f;
    private float OrbitPosStiffness => _config != null ? _config.orbitPosStiffness : 25f;
    private float OrbitVelDamping => _config != null ? _config.orbitVelDamping : 8f;
    private float OrbitMinSpeed => _config != null ? _config.orbitMinSpeed : 6f;
    private float OrbitTangentialAccel => _config != null ? _config.orbitTangentialAccel : 10f;
    private float OrbitExitImpulse => _config != null ? _config.orbitExitImpulse : 220f;

    private float RepulsionImpulse => _config != null ? _config.repulsionImpulse : 240f;
    private float RepulsionNearCenterFactor => _config != null ? _config.repulsionNearCenterFactor : 1.5f;

    private float EdgeMinFactor => _config != null ? _config.edgeMinFactor : 0.25f;

    private Color PlusColor => _config != null ? _config.plusColor : new Color(0.30f, 0.64f, 1f);
    private Color MinusColor => _config != null ? _config.minusColor : new Color(1f, 0.42f, 0.42f);

    #endregion

    #region State & Services

    public int Polarity { get; private set; } = -1;

    private Rigidbody2D _rb;
    private IInputService _input;
    private IGameEvents _events;
    private LevelManager _levelManager;

    private readonly List<MagneticNode> _activeNodes = new();

    private Coroutine _pulseRoutine;
    private Coroutine _absorbRoutine;

    private MagneticNode _spawnNode;
    private MagneticNode _lastVisitedNode;

    // Орбита
    private MagneticNode _orbitNode;
    private bool _isOrbiting;
    private float _orbitRadius;
    private float _orbitSpinSign = 1f; // знак вращения: +1 / -1

    private float _visualAngle;
    private bool _hasMagneticInfluence;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearVelocity = new Vector2(0f, StartUpSpeed);

        _visualAngle = _rb.rotation;

        UpdateVisual();
        InitializeServices();

        ServiceLocator.WhenAvailable<LevelManager>(lm => _levelManager = lm);
    }

    private void OnDisable()
    {
        if(_input != null)
            _input.TogglePolarity -= OnToggle;
    }

    private void FixedUpdate()
    {
        _hasMagneticInfluence = false;

        if(_isOrbiting)
        {
            UpdateOrbit();
            _events?.FireSpeedChanged(_rb.linearVelocity.magnitude);
            RotateTowardsVelocity();
            return;
        }

        ApplyMagnetForces();
        ApplyDragAndCruise();
        ClampSpeed();

        TryAutoOrbit();
        ApplyAutoPilot();

        _events?.FireSpeedChanged(_rb.linearVelocity.magnitude);
        RotateTowardsVelocity();
    }

    #endregion

    #region Service Init

    private void InitializeServices()
    {
        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.TogglePolarity += OnToggle;
        });

        ServiceLocator.WhenAvailable<IGameEvents>(ev =>
        {
            _events = ev;
        });
    }

    #endregion

    #region Public API (Nodes & Polarity & Portal)

    public void AddNode(MagneticNode node)
    {
        if(node == null)
            return;

        if(!_activeNodes.Contains(node))
            _activeNodes.Add(node);
    }

    public void RemoveNode(MagneticNode node)
    {
        if(node == null)
            return;

        _activeNodes.Remove(node);
    }

    public void OnNodeTriggerExit(MagneticNode node)
    {
        if(node == null)
            return;

        // Если сейчас крутимся вокруг этой ноды — не удаляем её,
        // иначе начнётся дергание логики
        if(_isOrbiting && _orbitNode == node)
            return;

        RemoveNode(node);
    }

    public void SetPortalTarget(Transform portal)
    {
        _portalTarget = portal;
    }

    public void RegisterSpawnNode(MagneticNode node)
    {
        _spawnNode = node;
    }

    public void RegisterVisitedNode(MagneticNode node)
    {
        if(node == null)
            return;

        _lastVisitedNode = node;
    }

    public void AbsorbIntoPortal(Vector3 portalPosition, float duration = 1.2f)
    {
        if(_absorbRoutine != null)
            return;

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

    public void TogglePolarity()
    {
        // Меняем полярность и визуал
        Polarity *= -1;
        UpdateVisual();

        if(_playerSfx)
            _playerSfx.OnSwitchPolarity();

        if(_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);

        _pulseRoutine = StartCoroutine(PulseInner());

        _events?.FirePolarityChanged(Polarity);

        // Если уже на орбите — решаем, слезать или остаться
        if(_isOrbiting && _orbitNode != null)
        {
            int relation = _orbitNode.Charge * Polarity;

            if(relation > 0)
            {
                // стала одинаковая полярность -> слиншот
                ExitOrbit(true);
            }

            return;
        }

        // Не на орбите — обычная логика: либо орбита, либо отталкивание
        HandlePolarityContext();
    }

    #endregion

    #region Input Handlers

    private void OnToggle()
    {
        if(_levelManager != null && _levelManager.State != GameState.Playing)
            return;

        TogglePolarity();
    }

    #endregion

    #region Orbit & Repulsion

    private void HandlePolarityContext()
    {
        MagneticNode node = FindNearestActiveNodeInRange();
        if(node == null)
            return;

        Vector2 p = _rb.position;
        Vector2 toNode = node.Position - p;
        float dist = toNode.magnitude;
        if(dist <= 0.001f || dist > node.Radius)
            return;

        int relation = node.Charge * Polarity;

        if(relation < 0)
        {
            // противоположные заряды -> орбита
            TryEnterOrbit(node);
        }
        else
        {
            // одинаковые заряды -> отталкивание
            ApplyRepulsionBurst(node, dist);
        }
    }

    private MagneticNode FindNearestActiveNodeInRange()
    {
        if(_activeNodes.Count == 0)
            return null;

        Vector2 p = _rb.position;
        MagneticNode best = null;
        float bestSqr = float.MaxValue;

        for(int i = 0; i < _activeNodes.Count; i++)
        {
            MagneticNode n = _activeNodes[i];
            if(n == null)
                continue;

            Vector2 d = n.Position - p;
            float sqr = d.sqrMagnitude;
            if(sqr > n.Radius * n.Radius)
                continue;

            if(sqr < bestSqr)
            {
                bestSqr = sqr;
                best = n;
            }
        }

        return best;
    }

    public void TryEnterOrbit(MagneticNode node)
    {
        if(_isOrbiting || node == null)
            return;

        if(node.Charge * Polarity >= 0)
            return;

        Vector2 center = node.Position;
        Vector2 r = (Vector2)_rb.position - center;   // player - center
        float dist = r.magnitude;
        if(dist <= 0.001f)
            return;

        float captureRadius = node.Radius * OrbitCaptureFactor;
        if(dist > captureRadius)
            return;

        _isOrbiting = true;
        _orbitNode = node;

        _orbitRadius = Mathf.Min(OrbitRadiusUnits, captureRadius);

        // Знак орбиты — по угловому моменту Lz = (r x v).z
        Vector2 v = _rb.linearVelocity;
        float Lz = r.x * v.y - r.y * v.x;

        if(Mathf.Abs(Lz) > 0.01f)
            _orbitSpinSign = Mathf.Sign(Lz);
        else
            _orbitSpinSign = 1f;
    }

    private void UpdateOrbit()
    {
        if(_orbitNode == null)
        {
            ExitOrbit(false);
            return;
        }

        Vector2 center = _orbitNode.Position;
        Vector2 r = (Vector2)_rb.position - center;    // player - center
        float dist = r.magnitude;
        if(dist <= 0.001f)
        {
            ExitOrbit(false);
            return;
        }

        Vector2 radialDir = r / dist;
        Vector2 v = _rb.linearVelocity;

        float radiusError = dist - _orbitRadius;
        float radialSpeed = Vector2.Dot(v, radialDir);

        // Радиальная пружина + демпфер
        Vector2 radialForce =
            (-radiusError * OrbitPosStiffness - radialSpeed * OrbitVelDamping) * radialDir;

        // Касательная: базис CCW, затем знак вращения
        Vector2 tangentBase = new(-radialDir.y, radialDir.x);
        Vector2 tangentDir = tangentBase * _orbitSpinSign;

        float tangentialSpeed = Vector2.Dot(v, tangentDir);
        float absTangential = Mathf.Abs(tangentialSpeed);

        Vector2 tangentialForce = Vector2.zero;

        if(absTangential < OrbitMinSpeed)
        {
            float delta = OrbitMinSpeed - absTangential;
            tangentialForce = tangentDir * (delta * OrbitTangentialAccel);
        }

        _rb.AddForce(radialForce + tangentialForce, ForceMode2D.Force);
    }

    private void ExitOrbit(bool withImpulse)
    {
        if(!_isOrbiting)
            return;

        if(withImpulse && _orbitNode != null)
        {
            Vector2 center = _orbitNode.Position;
            Vector2 r = (Vector2)_rb.position - center;
            float dist = r.magnitude;

            if(dist > 0.001f)
            {
                Vector2 radialDir = r / dist;
                Vector2 tangentBase = new(-radialDir.y, radialDir.x);
                Vector2 tangentDir = tangentBase * _orbitSpinSign;

                _rb.AddForce(tangentDir.normalized * OrbitExitImpulse, ForceMode2D.Impulse);
            }
        }

        _isOrbiting = false;
        _orbitNode = null;
    }

    private void ApplyRepulsionBurst(MagneticNode node, float dist)
    {
        Vector2 fromNode = _rb.position - node.Position;
        if(fromNode.sqrMagnitude < 1e-4f)
            fromNode = Random.insideUnitCircle.normalized;
        else
            fromNode /= dist;

        float t = 1f - Mathf.Clamp01(dist / node.Radius);
        float factor = Mathf.Lerp(1f, RepulsionNearCenterFactor, t);

        _rb.AddForce(fromNode * (RepulsionImpulse * factor), ForceMode2D.Impulse);
    }

    #endregion

    #region Physics: Magnet & Movement

    private void ApplyMagnetForces()
    {
        if(_activeNodes.Count == 0)
            return;

        Vector2 totalF = Vector2.zero;
        Vector2 p = _rb.position;

        for(int i = 0; i < _activeNodes.Count; i++)
        {
            MagneticNode n = _activeNodes[i];
            if(n == null)
                continue;

            Vector2 d = n.Position - p;
            float dist = d.magnitude;
            if(dist < 1e-3f)
                continue;

            float radius = n.Radius;
            if(dist >= radius)
                continue;

            float t = 1f - dist / radius;
            t = Mathf.Clamp01(t);
            t *= t;

            if(t > 0f && t < EdgeMinFactor)
                t = EdgeMinFactor;

            int sign = -Polarity * n.Charge;
            float fMag = n.Strength * t * sign;

            Vector2 dir = d / dist;
            totalF += dir * fMag;
        }

        if(totalF.sqrMagnitude > 0f)
            _hasMagneticInfluence = true;

        _rb.AddForce(totalF, ForceMode2D.Force);
    }

    private void ApplyDragAndCruise()
    {
        const float dragPerSec = 0.995f;
        float drag = Mathf.Pow(dragPerSec, Time.fixedDeltaTime * 60f);
        _rb.linearVelocity *= drag;

        float sp = _rb.linearVelocity.magnitude;

        if(sp < CruiseMin)
        {
            Vector2 dir = sp > 0.01f ? (_rb.linearVelocity / sp) : Vector2.up;
            _rb.AddForce(dir * CruiseAccel, ForceMode2D.Force);
        }
    }

    private void ClampSpeed()
    {
        float sp = _rb.linearVelocity.magnitude;
        if(sp > MaxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * MaxSpeed;
    }

    private void TryAutoOrbit()
    {
        if(_isOrbiting)
            return;

        MagneticNode node = FindNearestActiveNodeInRange();
        if(node == null)
            return;

        if(node.Charge * Polarity >= 0)
            return;

        TryEnterOrbit(node);
    }

    private void ApplyAutoPilot()
    {
        // пока хоть какая-то нода тянет/толкает или есть орбита, автопилот не лезет
        if(_hasMagneticInfluence || _isOrbiting)
            return;

        if(_levelManager != null && _levelManager.State != GameState.Playing)
            return;

        Transform target = null;

        if(_lastVisitedNode != null)
            target = _lastVisitedNode.transform;
        else if(_spawnNode != null)
            target = _spawnNode.transform;
        else if(_portalTarget != null)
            target = _portalTarget;

        if(target == null)
            return;

        Vector2 p = _rb.position;
        Vector2 t = target.position;
        Vector2 toTarget = t - p;
        float dist = toTarget.magnitude;

        if(dist < PortalMagnetStopDistance || dist > PortalMagnetMaxDistance)
            return;

        Vector2 dir = toTarget / Mathf.Max(dist, 0.001f);
        _rb.AddForce(dir * PortalMagnetForce, ForceMode2D.Force);
    }

    private void RotateTowardsVelocity()
    {
        if(_rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        Vector2 v = _rb.linearVelocity;
        if(v.sqrMagnitude < MinSpeedForRotate * MinSpeedForRotate)
            return;

        float targetAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;

        _visualAngle = Mathf.MoveTowardsAngle(
            _visualAngle,
            targetAngle,
            TurnSpeedDeg * Time.fixedDeltaTime
        );

        _rb.MoveRotation(_visualAngle);
    }

    #endregion

    #region Visual FX

    private void UpdateVisual()
    {
        Color c = (Polarity > 0) ? PlusColor : MinusColor;

        if(_bodyInner)
            _bodyInner.color = c;

        if(_trail)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(0f, 1f) }
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

    #endregion

    #region Portal Absorption

    private IEnumerator AbsorbRoutine(Vector3 portalPosition, float duration)
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;
        const float spinSpeed = 360f;

        float t = 0f;
        while(t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = 1f - Mathf.Pow(1f - k, 2f);

            transform.position = Vector3.Lerp(startPos, portalPosition, ease);
            transform.RotateAround(portalPosition, Vector3.forward, -spinSpeed * Time.deltaTime);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);

            yield return null;
        }

        transform.localScale = Vector3.zero;

        var sr = GetComponentInChildren<SpriteRenderer>();
        if(sr)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);

        _absorbRoutine = null;
        gameObject.SetActive(false);
    }

    #endregion
}
