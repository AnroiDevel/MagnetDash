using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public sealed class UiButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [Header("SFX")]
    [SerializeField] private SfxEvent _clickSfx;
    [SerializeField] private SfxEvent _hoverSfx;
    [SerializeField] private SfxEvent _downSfx;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if(_button != null)
            _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if(_clickSfx != null)
            Audio.Play(_clickSfx);           // UI-клик
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_hoverSfx != null)
            Audio.Play(_hoverSfx);           // наведение
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(_downSfx != null)
            Audio.Play(_downSfx);            // нажатие
    }
}
