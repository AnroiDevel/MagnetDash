using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShopCurrencyView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _amountLabel;

    private ICurrencyService _currency;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ICurrencyService>(BindCurrency);
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<ICurrencyService>(BindCurrency);
        UnbindCurrency();
    }

    private void BindCurrency(ICurrencyService currency)
    {
        if(ReferenceEquals(_currency, currency))
            return;

        UnbindCurrency();
        _currency = currency;
        _currency.AmountChanged += OnAmountChanged;

        Refresh();
    }

    private void UnbindCurrency()
    {
        if(_currency == null)
            return;

        _currency.AmountChanged -= OnAmountChanged;
        _currency = null;
    }

    private void OnAmountChanged(int _) => Refresh();

    private void Refresh()
    {
        if(_amountLabel == null || _currency == null)
            return;

        _amountLabel.text = _currency.Amount.ToString();
    }
}
