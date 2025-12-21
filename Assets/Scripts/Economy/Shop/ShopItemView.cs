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
    [SerializeField] private GameObject _activeMarker;
    [SerializeField] private GameObject _lockedOverlay;

    private IShopService _shop;
    private ICurrencyService _currency;

    private bool _shopBound;
    private bool _currencyBound;
    private bool _buttonsBound;

    private void Awake()
    {
        if(_skin == null || string.IsNullOrEmpty(_skin.id))
        {
            Debug.LogError("[ShopItemView] Skin is missing or has empty id.", this);
            enabled = false;
            return;
        }

        BindStaticUI();
        SetAllInactive();
    }

    private void OnEnable()
    {
        // currency
        if(ServiceLocator.TryGet<ICurrencyService>(out var currency))
            OnCurrencyReady(currency);
        else
            ServiceLocator.WhenAvailable<ICurrencyService>(OnCurrencyReady);

        // shop
        if(ServiceLocator.TryGet<IShopService>(out var shop))
            OnShopReady(shop);
        else
            ServiceLocator.WhenAvailable<IShopService>(OnShopReady);

        BindButtonsOnce();
        RefreshState();
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<IShopService>(OnShopReady);
        ServiceLocator.Unsubscribe<ICurrencyService>(OnCurrencyReady);

        if(_shop != null && _shopBound)
        {
            _shop.CurrentSkinChanged -= OnSkinChanged;
            _shopBound = false;
        }

        if(_currency != null && _currencyBound)
        {
            _currency.AmountChanged -= OnAmountChanged;
            _currencyBound = false;
        }

        if(_buyButton != null)
            _buyButton.onClick.RemoveListener(OnBuyClicked);
        if(_selectButton != null)
            _selectButton.onClick.RemoveListener(OnSelectClicked);

        _buttonsBound = false;
    }

    private void OnShopReady(IShopService shop)
    {
        if(shop == null)
            return;

        if(ReferenceEquals(_shop, shop) && _shopBound)
            return;

        if(_shop != null && _shopBound)
            _shop.CurrentSkinChanged -= OnSkinChanged;

        _shop = shop;
        _shop.CurrentSkinChanged += OnSkinChanged;
        _shopBound = true;

        RefreshState();
    }

    private void OnCurrencyReady(ICurrencyService currency)
    {
        if(currency == null)
            return;

        if(ReferenceEquals(_currency, currency) && _currencyBound)
            return;

        if(_currency != null && _currencyBound)
            _currency.AmountChanged -= OnAmountChanged;

        _currency = currency;
        _currency.AmountChanged += OnAmountChanged;
        _currencyBound = true;

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

    private void BindButtonsOnce()
    {
        if(_buttonsBound)
            return;

        if(_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);

        if(_selectButton != null)
            _selectButton.onClick.AddListener(OnSelectClicked);

        _buttonsBound = true;
    }

    private void RefreshState()
    {
        if(_shop == null || _currency == null)
        {
            SetAllInactive();
            return;
        }

        string id = _skin.id;

        bool active = _shop.CurrentSkinId == id;
        bool owned = _shop.IsOwned(id);

        // активный всегда "owned" (если вдруг данные не успели Ч UI не должен показывать Buy)
        if(active && !owned)
            owned = true;

        bool canBuy = _currency.CanSpend(_skin.price);

        bool showBuy = !owned;              // buy только если Ќ≈ owned
        bool showSelect = owned && !active; // select если owned и не активен

        if(_buyButton != null)
        {
            _buyButton.gameObject.SetActive(showBuy);
            _buyButton.interactable = showBuy && canBuy;
        }

        if(_selectButton != null)
        {
            _selectButton.gameObject.SetActive(showSelect);
            _selectButton.interactable = showSelect;
        }

        if(_activeMarker != null)
            _activeMarker.SetActive(active);

        if(_lockedOverlay != null)
            _lockedOverlay.SetActive(showBuy && !canBuy);
    }

    private void SetAllInactive()
    {
        if(_buyButton != null)
        {
            _buyButton.gameObject.SetActive(false);
            _buyButton.interactable = false;
        }

        if(_selectButton != null)
        {
            _selectButton.gameObject.SetActive(false);
            _selectButton.interactable = false;
        }

        if(_lockedOverlay != null)
            _lockedOverlay.SetActive(false);

        if(_activeMarker != null)
            _activeMarker.SetActive(false);
    }

    private void OnBuyClicked()
    {
        if(_shop == null)
            return;

        if(_shop.TryBuy(_skin.id))
            RefreshState();
    }

    private void OnSelectClicked()
    {
        if(_shop == null)
            return;

        if(_shop.TrySetCurrentSkin(_skin.id))
            RefreshState();
    }

    private void OnSkinChanged(string _) => RefreshState();
    private void OnAmountChanged(int _) => RefreshState();
}
