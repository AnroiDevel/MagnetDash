using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Graphic))]
public sealed class PortalArrow : MonoBehaviour
{
    #region Serialized Fields

    [Header("World / Camera")]
    [SerializeField] private Camera _worldCamera;

    [Header("UI")]
    [SerializeField] private float _edgePadding = 80f;

    #endregion

    #region State

    private RectTransform _rect;
    private RectTransform _canvasRect;
    private Canvas _rootCanvas;
    private Camera _uiCamera;

    private Transform _target;
    private Graphic _graphic;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _graphic = GetComponent<Graphic>();

        _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if(_rootCanvas != null)
        {
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _uiCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
        }

        if(_worldCamera == null)
            _worldCamera = Camera.main;

        // 1) Сразу пробуем привязаться (важно, если портал уже зарегистрирован)
        TryBindPortal();

        // 2) И дополнительно подписываемся на "появится позже"
        ServiceLocator.WhenAvailable<IPortal>(p => SetTarget(p != null ? p.Transform : null));
    }

    private void LateUpdate()
    {
        if(_graphic == null || _canvasRect == null)
            return;

        if(_worldCamera == null)
            _worldCamera = Camera.main;

        // Портал могли пересоздать на новом уровне — подхватываем заново
        if(_target == null)
            TryBindPortal();

        if(_target == null || _worldCamera == null)
        {
            _graphic.enabled = false;
            return;
        }

        Vector3 screenPos = _worldCamera.WorldToScreenPoint(_target.position);

        if(screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector3 viewportPos = _worldCamera.ScreenToViewportPoint(screenPos);

        bool portalOnScreen =
            viewportPos.z > 0f &&
            viewportPos.x > 0f && viewportPos.x < 1f &&
            viewportPos.y > 0f && viewportPos.y < 1f;

        if(portalOnScreen)
        {
            _graphic.enabled = false;
            return;
        }

        _graphic.enabled = true;

        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, _uiCamera, out Vector2 localPoint))
            return;

        Vector2 dir = localPoint.sqrMagnitude > 0.0001f ? localPoint.normalized : Vector2.up;

        float halfW = _canvasRect.rect.width * 0.5f - _edgePadding;
        float halfH = _canvasRect.rect.height * 0.5f - _edgePadding;

        halfW = Mathf.Max(halfW, 0f);
        halfH = Mathf.Max(halfH, 0f);

        float k = Mathf.Max(
            Mathf.Abs(dir.x) / Mathf.Max(halfW, 0.001f),
            Mathf.Abs(dir.y) / Mathf.Max(halfH, 0.001f)
        );

        if(k < 1f)
            k = 1f;

        Vector2 pos;
        pos.x = dir.x / k * halfW;
        pos.y = dir.y / k * halfH;

        _rect.anchoredPosition = pos;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _rect.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    #endregion

    #region Private

    private void TryBindPortal()
    {
        if(_target != null)
            return;

        if(ServiceLocator.TryGet<IPortal>(out var portal) && portal != null)
            _target = portal.Transform;
    }

    #endregion

    #region Public API

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    #endregion
}
