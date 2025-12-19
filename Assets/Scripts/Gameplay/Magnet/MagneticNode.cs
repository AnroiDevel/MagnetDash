using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class MagneticNode : MonoBehaviour
{
    [Header("Magnet")]
    [SerializeField] private int _charge = +1;        // +1 / -1
    [SerializeField] private float _radius = 4f;      // радиус поля
    [SerializeField] private float _strength = 20f;   // базовая сила

    public int Charge => _charge;
    public float Radius => _radius;
    public float Strength => _strength;
    public Vector2 Position => transform.position;

    private CircleCollider2D _collider;

    private void Awake()
    {
        CacheCollider();
        SyncColliderRadius();
    }

    private void Reset()
    {
        CacheCollider();
        if(_collider != null)
        {
            _collider.isTrigger = true;
            _collider.radius = _radius;
        }
    }

    private void OnValidate()
    {
        CacheCollider();
        SyncColliderRadius();
    }

    private void CacheCollider()
    {
        if(_collider == null)
            _collider = GetComponent<CircleCollider2D>();
    }

    private void SyncColliderRadius()
    {
        if(_collider == null)
            return;

        _collider.isTrigger = true;
        _collider.radius = _radius;
    }

    /// <summary>
    /// Инициализация параметров ноды в рантайме (для генератора уровней).
    /// </summary>
    public void InitRuntime(int charge, float radius, float strength)
    {
        _charge = charge;
        _radius = radius;
        _strength = strength;
        SyncColliderRadius();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.TryGetComponent<IMagneticNodeListener>(out var listener))
            return;

        listener.AddNode(this);
        listener.RegisterVisitedNode(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if(!other.TryGetComponent<PlayerMagnet>(out var player))
            return;

        player.OnNodeTriggerExit(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _charge > 0 ? Color.cyan : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
