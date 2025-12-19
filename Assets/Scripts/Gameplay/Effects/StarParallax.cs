using UnityEngine;

public sealed class StarParallax : MonoBehaviour
{
    [SerializeField] private Transform _camera;
    [SerializeField] private float _parallaxFactor = 0.02f;

    private Vector3 _lastCameraPos;

    private void Awake()
    {
        _lastCameraPos = _camera.position;
    }

    private void LateUpdate()
    {
        var delta = _camera.position - _lastCameraPos;
        transform.position += delta * _parallaxFactor;
        _lastCameraPos = _camera.position;
    }
}
