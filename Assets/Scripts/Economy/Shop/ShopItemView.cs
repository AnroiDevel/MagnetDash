using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class ShopItemView : MonoBehaviour
{
    [Header("Skin Data")]
    [SerializeField] private DroneSkinDefinition _skin;

    [Header("UI")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _priceLabel;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Button _selectButton;
    [SerializeField] private GameObject _activeMarker;   // Уј “»¬ЌќФ
    [SerializeField] private GameObject _lockedOverlay;  // затемнение если нет валюты (опционально)

    private IShopService _shop;
    private ICurrencyService _currency;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<IShopService>(OnShopReady);
        ServiceLocator.WhenAvailable<ICurrencyService>(OnCurrencyReady);
    }

    private void OnDisable()
    {
        if(_shop != null)
            _shop.CurrentSkinChanged -= OnSkinChanged;

        if(_currency != null)
            _currency.AmountChanged -= OnAmountChanged;

        _buyButton?.onClick.RemoveListener(OnBuyClicked);
        _selectButton?.onClick.RemoveListener(OnSelectClicked);
    }

    // ------------------------------------------
    // »Ќ»÷»јЋ»«ј÷»я
    // ------------------------------------------

    private void OnShopReady(IShopService shop)
    {
        _shop = shop;
        _shop.CurrentSkinChanged += OnSkinChanged;

        BindStaticUI();
        BindButtons();

        RefreshState();
    }

    private void OnCurrencyReady(ICurrencyService currency)
    {
        _currency = currency;
        _currency.AmountChanged += OnAmountChanged;

        RefreshState();
    }

    private void BindStaticUI()
    {
        if(_icon != null)
            _icon.sprite = _skin.icon;

        if(_nameLabel != null)
            _nameLabel.text = _skin.displayName;

        if(_priceLabel != null)
            _priceLabel.text = _skin.price.ToString();
    }

    private void BindButtons()
    {
        _buyButton?.onClick.AddListener(OnBuyClicked);
        _selectButton?.onClick.AddListener(OnSelectClicked);
    }

    // ------------------------------------------
    // UI ќЅЌќ¬Ћ≈Ќ»≈
    // ------------------------------------------

    private void RefreshState()
    {
        if(_shop == null || _currency == null)
            return;

        bool owned = _shop.IsOwned(_skin.id);
        bool active = _shop.CurrentSkinId == _skin.id;
        bool canBuy = _currency.CanSpend(_skin.price);

        //  нопки
        if(_buyButton != null)
        {
            _buyButton.gameObject.SetActive(!owned);
            _buyButton.interactable = canBuy;
        }

        if(_selectButton != null)
            _selectButton.gameObject.SetActive(owned && !active);

        if(_activeMarker != null)
            _activeMarker.SetActive(active);

        if(_lockedOverlay != null)
            _lockedOverlay.SetActive(!owned && !canBuy);
    }

    // ------------------------------------------
    // ќЅ–јЅќ“ ј  Ќќѕќ 
    // ------------------------------------------

    private void OnBuyClicked()
    {
        if(_shop.TryBuy(_skin.id))
        {
            // ≈сли купили Ч автоматически можно выбрать
            RefreshState();
        }
        else
        {
            Debug.Log("Not enough currency to buy.");
        }
    }

    private void OnSelectClicked()
    {
        if(_shop.TrySetCurrentSkin(_skin.id))
        {
            RefreshState();
        }
    }

    // ------------------------------------------
    // —ќЅџ“»я
    // ------------------------------------------

    private void OnSkinChanged(string _)
    {
        RefreshState();
    }

    private void OnAmountChanged(int _)
    {
        RefreshState();
    }
}
