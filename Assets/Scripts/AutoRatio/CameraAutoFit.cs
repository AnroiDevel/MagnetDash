using UnityEngine;

[ExecuteAlways]
public sealed class CameraAutoFit : MonoBehaviour
{
    public enum FitMode { Contain, Cover, FixedHeight, FixedWidth }

    [Header("Design area in world units (portrait)")]
    [SerializeField] private float _designWidth = 5.625f;
    [SerializeField] private float _designHeight = 10f;

    [Header("Behavior")]
    [SerializeField] private FitMode _mode = FitMode.Contain;
    [SerializeField] private Camera _cam;

    private int _lastW, _lastH;

    private void Reset()
    {
        _cam = GetComponent<Camera>();
        if(_cam)
            _cam.orthographic = true;
    }

    private void OnEnable()
    {
        if(_cam == null)
            _cam = GetComponent<Camera>();
        ForceApply();
//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.update += ForceApply;
//#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= ForceApply;
#endif
    }

    private void Update()
    {
        if(Screen.width != _lastW || Screen.height != _lastH)
            ForceApply();
    }

    private void ForceApply()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        Apply();
    }

    private void Apply()
    {
        if(!_cam)
            return;
        if(!_cam.orthographic)
            _cam.orthographic = true;
        if(Screen.width <= 0 || Screen.height <= 0)
            return;

        float targetAspect = _designWidth / _designHeight;

        // Базовое окно = safe area (в пикселях экрана)
        Rect sa = Screen.safeArea;
        if(sa.width <= 0f || sa.height <= 0f)
            sa = new Rect(0, 0, Screen.width, Screen.height);

        // Нормализованный safe area для camera.rect
        float ax = sa.xMin / Screen.width;
        float ay = sa.yMin / Screen.height;
        float aw = sa.width / Screen.width;
        float ah = sa.height / Screen.height;

        switch(_mode)
        {
            case FitMode.Contain:
            {
                _cam.orthographicSize = _designHeight * 0.5f;

                // Аспект в пределах safe area
                float windowAspect = sa.width / sa.height;

                // Начинаем с rect, совпадающего с safe area
                Rect r = new Rect(ax, ay, aw, ah);

                if(windowAspect > targetAspect)
                {
                    // wide → вертикальные поля внутри safe area
                    float wNorm = (targetAspect / windowAspect) * aw; // доля ширины safe area
                    float x = ax + (aw - wNorm) * 0.5f;
                    r = new Rect(x, ay, wNorm, ah);
                }
                else
                {
                    // tall → горизонтальные поля внутри safe area
                    float hNorm = (windowAspect / targetAspect) * ah; // доля высоты safe area
                    float y = ay + (ah - hNorm) * 0.5f;
                    r = new Rect(ax, y, aw, hNorm);
                }

                _cam.rect = r;
                break;
            }

            case FitMode.Cover:
            {
                _cam.rect = new Rect(ax, ay, aw, ah); // заполняем весь safe area
                float byH = _designHeight * 0.5f;
                float windowAspect = sa.width / sa.height;
                float byW = (_designWidth / windowAspect) * 0.5f;
                _cam.orthographicSize = Mathf.Min(byH, byW);
                break;
            }

            case FitMode.FixedHeight:
                _cam.rect = new Rect(ax, ay, aw, ah);
                _cam.orthographicSize = _designHeight * 0.5f;
                break;

            case FitMode.FixedWidth:
            {
                _cam.rect = new Rect(ax, ay, aw, ah);
                float windowAspect = sa.width / sa.height;
                _cam.orthographicSize = (_designWidth / windowAspect) * 0.5f;
                break;
            }
        }
    }
}
