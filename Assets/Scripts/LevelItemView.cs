using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class LevelItemView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _number;
    [SerializeField] private Image _frame;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _lockIcon;
    [SerializeField] private Image[] _stars; // 3 элемента (пустая/залитая через SpriteSwap или цвет)

    [Header("Sprites")]
    [SerializeField] private Sprite _frameNeutral;
    [SerializeField] private Sprite _frameLocked;
    [SerializeField] private Sprite _starEmpty;
    [SerializeField] private Sprite _starFilled;

    private Button _button;
    private int _buildIndex;
    private System.Action<int> _onClick;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if(_button)
            _button.onClick.AddListener(HandleClick);
    }
    private void OnDestroy() => _button?.onClick.RemoveAllListeners();

    public void Bind(int buildIndex, int number, int stars, bool unlocked, System.Action<int> onClick)
    {
        _buildIndex = buildIndex;
        _onClick = onClick;

        if(_number)
            _number.SetText(number.ToString());

        // звезды: 0..3
        for(int i = 0; i < _stars.Length; i++)
        {
            if(!_stars[i])
                continue;
            _stars[i].sprite = i < stars ? _starFilled : _starEmpty;
            _stars[i].color = Color.white;
        }

        bool locked = !unlocked;
        if(_lockIcon)
            _lockIcon.gameObject.SetActive(locked);
        if(_button)
            _button.interactable = !locked;

        // рамка/заливка
        if(_frame)
            _frame.sprite = locked ? _frameLocked : _frameNeutral;
        if(_fill)
            _fill.color = locked ? new Color(1, 1, 1, 0.55f) : Color.white;
    }

    private void HandleClick()
    {
        if(_button && !_button.interactable)
            return;
        _onClick?.Invoke(_buildIndex);
    }
}
