using UnityEngine;

[CreateAssetMenu(menuName = "Game/Player/PlayerMagnet Config")]
public sealed class PlayerMagnetConfig : ScriptableObject
{
    [Header("Movement")]
    public float startUpSpeed = 120f;
    public float maxSpeed = 360f;
    public float cruiseMin = 60f;
    public float cruiseAccel = 120f;

    [Header("Rotation")]
    public float turnSpeedDeg = 360f;
    public float minSpeedForRotate = 5f;

    [Header("Portal Magnet / AutoPilot")]
    public float portalMagnetForce = 5f;
    public float portalMagnetMaxDistance = 100f;
    public float portalMagnetStopDistance = 0.5f;

    [Header("Orbit")]
    public float orbitRadiusUnits = 2.5f;
    [Range(0f, 1f)] public float orbitCaptureFactor = 0.7f;
    public float orbitPosStiffness = 25f;
    public float orbitVelDamping = 8f;
    public float orbitMinSpeed = 6f;          // ћ»Ќ»ћјЋ№Ќјя скорость по орбите
    public float orbitTangentialAccel = 10f;  // Ќасколько активно разгон€ем до минимума
    public float orbitExitImpulse = 220f;

    [Header("Repulsion")]
    public float repulsionImpulse = 240f;
    public float repulsionNearCenterFactor = 1.5f;

    [Header("Magnet Falloff")]
    [Range(0f, 1f)] public float edgeMinFactor = 0.25f;

    [Header("FX")]
    public Color plusColor = new(0.30f, 0.64f, 1f);
    public Color minusColor = new(1f, 0.42f, 0.42f);
}
