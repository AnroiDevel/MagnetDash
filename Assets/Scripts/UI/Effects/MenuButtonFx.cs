using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class MenuButtonFx : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale")]
    [SerializeField] private float _normalScale = 1f;
    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _pressedScale = 0.96f;
    [SerializeField] private float _animationDuration = 0.12f;

    private RectTransform _rect;
    private Button _button;
    private Coroutine _routine;

    private bool _isOver;
    private bool _isPressed;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _button = GetComponent<Button>();
        _rect.localScale = Vector3.one * _normalScale;
    }

    private bool CanAnimate()
    {
        return _button == null || _button.interactable;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if(!CanAnimate())
            return;

        _isOver = true;
        if(_isPressed)
            return;

        Animate(_hoverScale);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if(!CanAnimate())
            return;

        _isOver = false;
        if(_isPressed)
            return;

        Animate(_normalScale);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if(!CanAnimate())
            return;

        _isPressed = true;
        Animate(_pressedScale);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if(!CanAnimate())
            return;

        _isPressed = false;

        float target = _isOver ? _hoverScale : _normalScale;
        Animate(target);
    }

    private void Animate(float scale)
    {
        if(_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(AnimRoutine(scale));
    }

    private IEnumerator AnimRoutine(float target)
    {
        float t = 0f;
        Vector3 start = _rect.localScale;
        Vector3 end = Vector3.one * target;

        float dur = Mathf.Max(0.01f, _animationDuration);

        while(t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            k = k * k * (3f - 2f * k); // smoothstep
            _rect.localScale = Vector3.Lerp(start, end, k);
            yield return null;
        }

        _rect.localScale = end;
        _routine = null;
    }

    private void Update()
    {
        // Если кнопку отключили — мгновенно сбрасываем scale
        if(_button != null && !_button.interactable)
        {
            if(_rect.localScale != Vector3.one * _normalScale)
                _rect.localScale = Vector3.one * _normalScale;
        }
    }
}
