using UnityEngine;

public sealed class PlayerOrbit
{
    private readonly PlayerMagnetContext _ctx;
    private readonly int _engineWearPerOrbitExit;

    private MagneticNode _orbitNode;
    private bool _isOrbiting;
    private float _orbitRadius;
    private float _orbitSpinSign = 1f;

    public bool IsOrbiting => _isOrbiting;

    public PlayerOrbit(PlayerMagnetContext ctx, int engineWearPerOrbitExit)
    {
        _ctx = ctx;
        _engineWearPerOrbitExit = engineWearPerOrbitExit;
    }

    public void Tick()
    {
        if(!_isOrbiting)
            return;

        if(_orbitNode == null)
        {
            Exit(false);
            return;
        }

        float orbitPosStiffness = Get(_ctx.Config, c => c.orbitPosStiffness, 25f);
        float orbitVelDamping = Get(_ctx.Config, c => c.orbitVelDamping, 8f);
        float orbitMinSpeed = Get(_ctx.Config, c => c.orbitMinSpeed, 6f);
        float orbitTangentialAccel = Get(_ctx.Config, c => c.orbitTangentialAccel, 10f);

        Vector2 center = _orbitNode.Position;
        Vector2 r = (Vector2)_ctx.Rb.position - center;
        float dist = r.magnitude;

        if(dist <= 0.001f)
        {
            Exit(false);
            return;
        }

        Vector2 radialDir = r / dist;
        Vector2 v = _ctx.Rb.linearVelocity;

        float radiusError = dist - _orbitRadius;
        float radialSpeed = Vector2.Dot(v, radialDir);

        Vector2 radialForce =
            (-radiusError * orbitPosStiffness - radialSpeed * orbitVelDamping) * radialDir;

        Vector2 tangentBase = new(-radialDir.y, radialDir.x);
        Vector2 tangentDir = tangentBase * _orbitSpinSign;

        float tangentialSpeed = Vector2.Dot(v, tangentDir);
        Vector2 tangentialForce = Vector2.zero;

        if(Mathf.Abs(tangentialSpeed) < orbitMinSpeed)
        {
            float delta = orbitMinSpeed - Mathf.Abs(tangentialSpeed);
            tangentialForce = tangentDir * (delta * orbitTangentialAccel);
        }

        _ctx.Rb.AddForce(radialForce + tangentialForce, ForceMode2D.Force);
    }

    public void TryAutoEnter(MagneticNode node)
    {
        if(_isOrbiting || node == null)
            return;

        if(node.Charge * _ctx.Polarity >= 0)
            return;

        TryEnter(node);
    }

    public void TryEnter(MagneticNode node)
    {
        if(_isOrbiting || node == null)
            return;

        if(node.Charge * _ctx.Polarity >= 0)
            return;

        float orbitCaptureFactor = Get(_ctx.Config, c => c.orbitCaptureFactor, 0.7f);
        float orbitRadiusUnits = Get(_ctx.Config, c => c.orbitRadiusUnits, 2.5f);

        Vector2 center = node.Position;
        Vector2 r = (Vector2)_ctx.Rb.position - center;
        float dist = r.magnitude;

        if(dist <= 0.001f)
            return;

        float captureRadius = node.Radius * orbitCaptureFactor;
        if(dist > captureRadius)
            return;

        _isOrbiting = true;
        _orbitNode = node;

        _orbitRadius = Mathf.Min(orbitRadiusUnits, captureRadius);

        Vector2 v = _ctx.Rb.linearVelocity;
        float Lz = r.x * v.y - r.y * v.x;
        _orbitSpinSign = (Mathf.Abs(Lz) > 0.01f) ? Mathf.Sign(Lz) : 1f;
    }

    public void Exit(bool withImpulse)
    {
        if(!_isOrbiting)
            return;

        if(withImpulse && _orbitNode != null)
        {
            float orbitExitImpulse = Get(_ctx.Config, c => c.orbitExitImpulse, 220f);

            Vector2 center = _orbitNode.Position;
            Vector2 r = (Vector2)_ctx.Rb.position - center;
            float dist = r.magnitude;

            if(dist > 0.001f)
            {
                Vector2 radialDir = r / dist;
                Vector2 tangentDir = new Vector2(-radialDir.y, radialDir.x) * _orbitSpinSign;

                _ctx.Rb.AddForce(tangentDir.normalized * orbitExitImpulse, ForceMode2D.Impulse);
                _ctx.Progress?.DamageEngine(_engineWearPerOrbitExit);
            }
        }

        _isOrbiting = false;
        _orbitNode = null;
    }

    public void ApplyRepulsionBurst(MagneticNode node, float dist)
    {
        float repulsionImpulse = Get(_ctx.Config, c => c.repulsionImpulse, 240f);
        float repulsionNearCenterFactor = Get(_ctx.Config, c => c.repulsionNearCenterFactor, 1.5f);

        Vector2 fromNode = _ctx.Rb.position - node.Position;
        fromNode = (fromNode.sqrMagnitude < 1e-4f) ? Random.insideUnitCircle.normalized : fromNode / dist;

        float t = 1f - Mathf.Clamp01(dist / node.Radius);
        float factor = Mathf.Lerp(1f, repulsionNearCenterFactor, t);

        _ctx.Rb.AddForce(fromNode * (repulsionImpulse * factor), ForceMode2D.Impulse);
    }

    public bool IsOrbitNode(MagneticNode node) => _isOrbiting && _orbitNode == node;
    public MagneticNode OrbitNode => _orbitNode;

    private static float Get(PlayerMagnetConfig cfg, System.Func<PlayerMagnetConfig, float> sel, float fallback)
        => cfg != null ? sel(cfg) : fallback;
}
