using System.Collections.Generic;
using UnityEngine;

public sealed class MagneticNode : MonoBehaviour
{
    [SerializeField] private int _charge = +1; // +1 (синий) / -1 (красный)

    private static readonly List<MagneticNode> _all = new();
    public static List<MagneticNode> All => _all;

    public int Charge => _charge;
    public Vector2 Position => (Vector2)transform.position;

    private void OnEnable() { _all.Add(this); }
    private void OnDisable() { _all.Remove(this); }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _charge > 0 ? new Color(0.3f, 0.64f, 1f, 0.6f) : new Color(1f, 0.42f, 0.42f, 0.6f);
        Gizmos.DrawSphere(transform.position, 0.2f);
    }
#endif
}
