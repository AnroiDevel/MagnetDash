using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMagnet : MonoBehaviour, IMagneticNodeListener
{
    [Header("Config")]
    [SerializeField] private PlayerMagnetConfig _config;

    [Header("Stars FX")]
    [SerializeField] private ParticleSystem _starCollectFxPrefab;

    [Header("Scene References")]
    [SerializeField] private Transform _portalTarget;

    [Header("Engine Wear")]
    [SerializeField] private int _engineWearPerOrbitExit = 1;

    [Header("Skin")]
    [SerializeField] private PlayerSkinView _skinView;

    public int Polarity => _ctx.Polarity;

    private Rigidbody2D _rb;
    private IInputService _input;
    private IProgressService _progress;
    private LevelManager _levelManager;
    private IGameEvents _events;

    private readonly PlayerMagnetContext _ctx = new();

    private PlayerMagnetForces _magnet;
    private PlayerMovement _movement;
    private PlayerOrbit _orbit;
    private PlayerAutoPilot _autoPilot;

    private Coroutine _absorbRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        float startUpSpeed = _config != null ? _config.startUpSpeed : 2f;
        _rb.linearVelocity = new Vector2(0f, startUpSpeed);

        if(_skinView == null)
            _skinView = GetComponentInChildren<PlayerSkinView>(true);

        _ctx.Rb = _rb;
        _ctx.Config = _config;
        _ctx.PortalTarget = _portalTarget;

        _magnet = new PlayerMagnetForces(_ctx);
        _movement = new PlayerMovement(_ctx, _rb.rotation);
        _orbit = new PlayerOrbit(_ctx, _engineWearPerOrbitExit);
        _autoPilot = new PlayerAutoPilot(_ctx);

        InitializeServices();
    }

    private void OnDestroy()
    {
        if(_input != null)
            _input.TogglePolarity -= OnToggle;

        ServiceLocator.Unsubscribe<IProgressService>(OnProgressAvailable);
    }

    private void FixedUpdate()
    {
        if(_levelManager != null && _levelManager.State != GameState.Playing)
            return;

        _ctx.HasMagneticInfluence = false;

        if(_orbit.IsOrbiting)
        {
            _orbit.Tick();
        }
        else
        {
            // 1. СНАЧАЛА пытаемся войти в орбиту
            var node = FindNearestActiveNodeInRange();
            _orbit.TryAutoEnter(node);

            // 2. Потом применяем силы
            _magnet.Tick();
            _movement.ApplyDragAndCruise();
            _movement.ClampSpeed();

            // 3. В конце автопилот
            _autoPilot.Tick();
        }

        _events?.FireSpeedChanged(_rb.linearVelocity.magnitude);
        _movement.RotateTowardsVelocity();
    }

    private void InitializeServices()
    {
        ServiceLocator.WhenAvailable<LevelManager>(lm => _levelManager = _ctx.LevelManager = lm);

        ServiceLocator.WhenAvailable<IInputService>(svc =>
        {
            _input = svc;
            _input.TogglePolarity += OnToggle;
        });

        ServiceLocator.WhenAvailable<IGameEvents>(ev => _events = _ctx.Events = ev);
        ServiceLocator.WhenAvailable<IProgressService>(OnProgressAvailable);
    }

    private void OnProgressAvailable(IProgressService progress)
    {
        _progress = progress;
        _ctx.Progress = progress;
    }

    public void AddNode(MagneticNode node)
    {
        if(node == null || _ctx.ActiveNodes.Contains(node))
            return;

        _ctx.ActiveNodes.Add(node);

        if(!_orbit.IsOrbiting && node.Charge * _ctx.Polarity < 0)
            _orbit.TryEnter(node);
    }

    public void RemoveNode(MagneticNode node)
    {
        if(node != null)
            _ctx.ActiveNodes.Remove(node);
    }

    public void OnNodeTriggerExit(MagneticNode node)
    {
        if(node == null)
            return;

        if(_orbit.IsOrbitNode(node))
            return;

        RemoveNode(node);
    }

    public void RegisterSpawnNode(MagneticNode node) => _ctx.SpawnNode = node;
    public void RegisterVisitedNode(MagneticNode node) { if(node != null) _ctx.LastVisitedNode = node; }

    public void SetPortalTarget(Transform portal)
    {
        _portalTarget = portal;
        _ctx.PortalTarget = portal;
    }

    public void OnStarPickup(Vector3 worldPos)
    {
        _levelManager?.CollectStar(worldPos);

        if(_starCollectFxPrefab != null)
            Instantiate(_starCollectFxPrefab, worldPos, Quaternion.identity);
    }

    public void TogglePolarity()
    {
        _ctx.Polarity *= -1;
        _events?.FirePolarityChanged(_ctx.Polarity);

        if(_orbit.IsOrbiting && _orbit.OrbitNode != null)
        {
            int relation = _orbit.OrbitNode.Charge * _ctx.Polarity;
            if(relation > 0)
                _orbit.Exit(true);

            return;
        }

        HandlePolarityContext();
    }

    private void OnToggle()
    {
        if(_levelManager != null && _levelManager.State != GameState.Playing)
            return;

        TogglePolarity();
    }

    private void HandlePolarityContext()
    {
        var node = FindNearestActiveNodeInRange();
        if(node == null)
            return;

        Vector2 p = _rb.position;
        Vector2 toNode = node.Position - p;
        float dist = toNode.magnitude;

        if(dist <= 0.001f || dist > node.Radius)
            return;

        int relation = node.Charge * _ctx.Polarity;

        if(relation < 0)
            _orbit.TryEnter(node);
        else
            _orbit.ApplyRepulsionBurst(node, dist);
    }

    private MagneticNode FindNearestActiveNodeInRange()
    {
        var nodes = _ctx.ActiveNodes;
        if(nodes.Count == 0)
            return null;

        Vector2 p = _rb.position;
        MagneticNode best = null;
        float bestSqr = float.MaxValue;

        foreach(var n in nodes)
        {
            if(n == null)
                continue;

            Vector2 d = n.Position - p;
            float sqr = d.sqrMagnitude;

            float r = n.Radius;
            if(sqr > r * r)
                continue;

            if(sqr < bestSqr)
            {
                bestSqr = sqr;
                best = n;
            }
        }

        return best;
    }

    public void AbsorbIntoPortal(Vector3 portalPosition, float duration = 1.2f)
    {
        if(_absorbRoutine != null)
            return;

        if(ServiceLocator.TryGet<ILevelFlow>(out var flow))
            flow.CompleteLevel();
        else
            Debug.LogError("[PlayerMagnet] ILevelFlow not found.");

        _absorbRoutine = StartCoroutine(AbsorbRoutine(portalPosition, duration));
    }

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
        gameObject.SetActive(false);

        _absorbRoutine = null;
    }
}
