using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector2 _offset = new(0f, 2f);
    [SerializeField] private float _smoothTime = 0.2f;

    [Header("Limits")]
    [SerializeField] private bool _lockX = true;
    [SerializeField] private float _minY = -10f;
    [SerializeField] private float _maxY = 100f;

    private Vector3 _velocity;
    private IPlayerRegistry _registry;


    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IPlayerRegistry>(r =>
        {
            _registry = r;
            _registry.PlayerSpawned += SetTarget;

            if(_registry.Current != null)
                SetTarget(_registry.Current);
        });
    }

    private void OnDisable()
    {
        if(_registry != null)
            _registry.PlayerSpawned -= SetTarget;
    }

    private void LateUpdate()
    {
        if(_target == null)
            return;

        var current = transform.position;

        float targetX = _lockX ? current.x : _target.position.x;
        float targetY = _target.position.y;

        var targetPos = new Vector3(targetX, targetY, current.z);
        targetPos += (Vector3)_offset;

        targetPos.y = Mathf.Clamp(targetPos.y, _minY, _maxY);

        transform.position = Vector3.SmoothDamp(current, targetPos, ref _velocity, _smoothTime);
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }
}
