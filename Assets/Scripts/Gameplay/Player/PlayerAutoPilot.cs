using UnityEngine;

public sealed class PlayerAutoPilot
{
    private readonly PlayerMagnetContext _ctx;

    public PlayerAutoPilot(PlayerMagnetContext ctx) => _ctx = ctx;

    public void Tick()
    {
        if(_ctx.HasMagneticInfluence)
            return;

        Transform target = null;

        if(_ctx.LastVisitedNode != null)
            target = _ctx.LastVisitedNode.transform;
        else if(_ctx.SpawnNode != null)
            target = _ctx.SpawnNode.transform;
        else if(_ctx.PortalTarget != null)
            target = _ctx.PortalTarget;

        if(target == null)
            return;

        float force = Get(_ctx.Config, c => c.portalMagnetForce, 5f);
        float maxDist = Get(_ctx.Config, c => c.portalMagnetMaxDistance, 100f);
        float stopDist = Get(_ctx.Config, c => c.portalMagnetStopDistance, 0.5f);

        Vector2 p = _ctx.Rb.position;
        Vector2 tp = target.position;
        Vector2 toTarget = tp - p;
        float dist = toTarget.magnitude;

        if(dist < stopDist || dist > maxDist)
            return;

        _ctx.Rb.AddForce(toTarget.normalized * force, ForceMode2D.Force);
    }

    private static float Get(PlayerMagnetConfig cfg, System.Func<PlayerMagnetConfig, float> sel, float fallback)
        => cfg != null ? sel(cfg) : fallback;
}
