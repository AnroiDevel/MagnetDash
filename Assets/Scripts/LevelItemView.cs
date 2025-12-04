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
    [SerializeField] private Image[] _stars; // 3 элемента

    [Header("Sprites")]
    [SerializeField] private Sprite _frameNeutral;
    [SerializeField] private Sprite _frameLocked;
    [SerializeField] private Sprite _starEmpty;
    [SerializeField] private Sprite _starFilled;

    private Button _button;
    private int _levelId;                    // может быть buildIndex >= 0 или id < 0 (JSON)
    private System.Action<int> _onClick;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if(_button != null)
            _button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if(_button != null)
            _button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(int levelId, int number, int stars, bool unlocked, System.Action<int> onClick)
    {
        _levelId = levelId;
        _onClick = onClick;

        if(_number != null)
            _number.SetText(number.ToString());

        // звезды: 0..3
        if(_stars != null)
        {
            for(int i = 0; i < _stars.Length; i++)
            {
                if(_stars[i] == null)
                    continue;

                _stars[i].sprite = i < stars ? _starFilled : _starEmpty;
                _stars[i].color = Color.white;
            }
        }

        bool locked = !unlocked;

        if(_lockIcon != null)
            _lockIcon.gameObject.SetActive(locked);

        if(_button != null)
            _button.interactable = !locked;

        if(_frame != null)
            _frame.sprite = locked ? _frameLocked : _frameNeutral;

        if(_fill != null)
            _fill.color = locked ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
    }

    private void HandleClick()
    {
        if(_button != null && !_button.interactable)
            return;

        _onClick?.Invoke(_levelId);
    }
}
