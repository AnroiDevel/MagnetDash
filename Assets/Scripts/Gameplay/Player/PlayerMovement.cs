using UnityEngine;

public sealed class PlayerMovement
{
    private readonly PlayerMagnetContext _ctx;
    private float _visualAngle;

    public PlayerMovement(PlayerMagnetContext ctx, float startAngle)
    {
        _ctx = ctx;
        _visualAngle = startAngle;
    }

    public void ApplyDragAndCruise()
    {
        float cruiseMin = Get(_ctx.Config, c => c.cruiseMin, 5f);
        float cruiseAccel = Get(_ctx.Config, c => c.cruiseAccel, 10f);

        const float dragPerSec = 0.995f;
        float drag = Mathf.Pow(dragPerSec, Time.fixedDeltaTime * 60f);
        _ctx.Rb.linearVelocity *= drag;

        float sp = _ctx.Rb.linearVelocity.magnitude;
        if(sp < cruiseMin)
        {
            Vector2 dir = sp > 0.01f ? (_ctx.Rb.linearVelocity / sp) : Vector2.up;
            _ctx.Rb.AddForce(dir * cruiseAccel, ForceMode2D.Force);
        }
    }

    public void ClampSpeed()
    {
        float baseMax = Get(_ctx.Config, c => c.maxSpeed, 10f);
        float power = _ctx.Progress?.EnginePower ?? 1f;
        float maxSpeed = baseMax * power;

        float sp = _ctx.Rb.linearVelocity.magnitude;
        if(sp > maxSpeed)
            _ctx.Rb.linearVelocity = _ctx.Rb.linearVelocity.normalized * maxSpeed;
    }

    public void RotateTowardsVelocity()
    {
        float turnSpeedDeg = Get(_ctx.Config, c => c.turnSpeedDeg, 360f);
        float minSpeedForRotate = Get(_ctx.Config, c => c.minSpeedForRotate, 1f);

        Vector2 v = _ctx.Rb.linearVelocity;
        if(v.sqrMagnitude < minSpeedForRotate * minSpeedForRotate)
            return;

        float targetAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;

        _visualAngle = Mathf.MoveTowardsAngle(
            _visualAngle,
            targetAngle,
            turnSpeedDeg * Time.fixedDeltaTime
        );

        _ctx.Rb.MoveRotation(_visualAngle);
    }

    private static float Get(PlayerMagnetConfig cfg, System.Func<PlayerMagnetConfig, float> sel, float fallback)
        => cfg != null ? sel(cfg) : fallback;
}
