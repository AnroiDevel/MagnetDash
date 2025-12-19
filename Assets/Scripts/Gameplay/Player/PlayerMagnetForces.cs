using UnityEngine;

public sealed class PlayerMagnetForces
{
    private readonly PlayerMagnetContext _ctx;

    public PlayerMagnetForces(PlayerMagnetContext ctx) => _ctx = ctx;

    public void Tick()
    {
        var nodes = _ctx.ActiveNodes;
        if(nodes.Count == 0)
            return;

        Vector2 totalF = Vector2.zero;
        Vector2 p = _ctx.Rb.position;

        float edgeMin = Get(_ctx.Config, c => c.edgeMinFactor, 0.25f);

        foreach(var n in nodes)
        {
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

            if(t > 0f && t < edgeMin)
                t = edgeMin;

            int sign = -_ctx.Polarity * n.Charge;
            float fMag = n.Strength * t * sign;

            totalF += (d / dist) * fMag;
        }

        if(totalF.sqrMagnitude > 0f)
            _ctx.HasMagneticInfluence = true;

        _ctx.Rb.AddForce(totalF, ForceMode2D.Force);
    }

    private static float Get(PlayerMagnetConfig cfg, System.Func<PlayerMagnetConfig, float> sel, float fallback)
        => cfg != null ? sel(cfg) : fallback;
}
