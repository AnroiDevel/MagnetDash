using UnityEngine;

[DisallowMultipleComponent]
public sealed class PremiumCheapStarBackground : MonoBehaviour
{
    [System.Serializable]
    private sealed class Layer
    {
        public SpriteRenderer renderer;
        [Range(0f, 0.5f)] public float parallax = 0.05f;

        [Header("Twinkle")]
        [Range(0f, 0.25f)] public float twinkleAmount = 0.08f; // 0.05–0.12 обычно
        [Range(0.05f, 2f)] public float twinkleSpeed = 0.5f;   // разная на слой
        [Range(0f, 10f)] public float phase = 0f;              // чтобы не синхронились
    }

    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private Layer[] _layers;

    private Vector3 _cameraStart;
    private Vector3[] _layerStart;
    private MaterialPropertyBlock _mpb;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if(_cameraTransform == null)
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;

        _cameraStart = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;

        _layerStart = new Vector3[_layers.Length];
        for(int i = 0; i < _layers.Length; i++)
        {
            var r = _layers[i].renderer;
            _layerStart[i] = r != null ? r.transform.position : Vector3.zero;

            if(_layers[i].phase <= 0f)
                _layers[i].phase = Random.value * 10f;
        }

        _mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if(_cameraTransform == null)
            return;

        var delta = _cameraTransform.position - _cameraStart;
        delta.z = 0f;

        var t = Time.unscaledTime; // чтобы фон жил даже на паузе

        for(int i = 0; i < _layers.Length; i++)
        {
            var layer = _layers[i];
            var r = layer.renderer;
            if(r == null)
                continue;

            // Параллакс
            r.transform.position = _layerStart[i] + delta * layer.parallax;

            // “Премиальный” трюк без шейдера:
            // слой НЕ мигает синхронно, потому что у каждого слоя своя фаза и скорость,
            // а амплитуда маленькая.
            var baseColor = r.color;
            var tw = 1f + Mathf.Sin((t + layer.phase) * layer.twinkleSpeed) * layer.twinkleAmount;

            _mpb.SetColor(ColorId, new Color(baseColor.r * tw, baseColor.g * tw, baseColor.b * tw, baseColor.a));
            r.SetPropertyBlock(_mpb);
        }
    }
}
