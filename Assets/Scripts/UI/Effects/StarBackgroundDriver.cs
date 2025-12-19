using UnityEngine;

[DisallowMultipleComponent]
public sealed class StarBackgroundDriver : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private SpriteRenderer[] _layers;

    [Header("Offset Source")]
    [SerializeField] private float _offsetScale = 0.02f;
    [SerializeField] private float _smooth = 8f;

    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private MaterialPropertyBlock _mpb;
    private Vector2 _offset;
    private Vector3 _prevCamPos;

    private void Awake()
    {
        if(_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;

        _mpb = new MaterialPropertyBlock();

        _prevCamPos = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;
    }

    private void LateUpdate()
    {
        if(_cameraTransform == null || _layers == null || _layers.Length == 0)
            return;

        var camPos = _cameraTransform.position;
        var delta = camPos - _prevCamPos;
        _prevCamPos = camPos;

        var target = _offset + new Vector2(delta.x, delta.y) * _offsetScale;
        _offset = Vector2.Lerp(_offset, target, 1f - Mathf.Exp(-_smooth * Time.unscaledDeltaTime));

        for(int i = 0; i < _layers.Length; i++)
        {
            var r = _layers[i];
            if(r == null)
                continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetVector(OffsetId, _offset);
            r.SetPropertyBlock(_mpb);
        }
    }
}
