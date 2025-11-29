using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class StarfieldParallax : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector2 _parallaxMultiplier = new(0.02f, 0.02f);

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int UVOffsetId = Shader.PropertyToID("_UVOffset");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        if(_camera == null)
        {
            _camera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if(_camera == null)
            return;

        Vector3 camPos = _camera.transform.position;
        Vector2 offset = (Vector2)camPos * _parallaxMultiplier;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetVector(UVOffsetId, offset);
        _renderer.SetPropertyBlock(_mpb);
    }
}
