using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PortalArrow : MonoBehaviour
{
    #region Serialized Fields

    [Header("World / Camera")]
    [SerializeField] private Camera _worldCamera;          // камера, которая рендерит мир (Player/Portal)

    [Header("UI")]
    [SerializeField] private float _edgePadding = 80f;     // отступ от краёв экрана в ui-пикселях

    #endregion

    #region State

    private RectTransform _rect;
    private RectTransform _canvasRect;                     // корневой RectTransform канваса
    private Canvas _rootCanvas;
    private Camera _uiCamera;                              // камера канваса (или null для Overlay)

    private Transform _target;                             // Transform портала
    private Graphic _graphic;                              // Image / любая Graphic

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

            switch(_rootCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    _uiCamera = null; // по правилам Unity
                    break;

                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    _uiCamera = _rootCanvas.worldCamera;
                    break;

                default:
                    _uiCamera = null;
                    break;
            }
        }


        // получаем портал через сервис
        ServiceLocator.WhenAvailable<IPortal>(p =>
        {
            SetTarget(p.Transform);
        });
    }

    private void LateUpdate()
    {
        if(_graphic == null)
            return;
        if(_worldCamera == null)
            _worldCamera = Camera.main;

        if(_target == null || _worldCamera == null || _canvasRect == null)
        {
            _graphic.enabled = false;
            return;
        }

        // --- 1. Позиция портала в screen-space ---

        Vector3 screenPos = _worldCamera.WorldToScreenPoint(_target.position);

        // Если портал позади камеры — зеркалим по X/Y, чтобы стрелка указывала в "правильную" сторону
        if(screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        // --- 2. Проверяем, в кадре ли портал (через viewport) ---

        Vector3 viewportPos = _worldCamera.ScreenToViewportPoint(screenPos);

        bool portalOnScreen =
            viewportPos.z > 0f &&
            viewportPos.x > 0f && viewportPos.x < 1f &&
            viewportPos.y > 0f && viewportPos.y < 1f;

        // Если портал в кадре — стрелка прячется
        if(portalOnScreen)
        {
            _graphic.enabled = false;
            return;
        }

        // Портал вне экрана — стрелка видима
        _graphic.enabled = true;

        // --- 3. Переводим screen → local (координаты канваса) ---

        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
               _canvasRect,
               new Vector2(screenPos.x, screenPos.y),
               _uiCamera,
               out Vector2 localPoint))
        {
            return;
        }

        // Центр канваса = (0, 0), направление на портал:
        Vector2 dir = localPoint.normalized;

        float halfW = _canvasRect.rect.width * 0.5f - _edgePadding;
        float halfH = _canvasRect.rect.height * 0.5f - _edgePadding;

        // --- 4. Прижимаем к прямоугольнику (края экрана) ---

        Vector2 pos = dir;
        float k = Mathf.Max(
            Mathf.Abs(pos.x) / Mathf.Max(halfW, 0.001f),
            Mathf.Abs(pos.y) / Mathf.Max(halfH, 0.001f)
        );
        if(k < 1f)
            k = 1f;

        pos.x = pos.x / k * halfW;
        pos.y = pos.y / k * halfH;

        _rect.anchoredPosition = pos;

        // --- 5. Поворот стрелки по направлению к порталу ---

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _rect.rotation = Quaternion.Euler(0f, 0f, angle - 90f); // если спрайт "смотрит вверх"
    }

    #endregion

    #region Public API

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    #endregion
}
