using TMPro;
using UnityEngine;

public sealed class ShopCurrencyView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _amountLabel;

    private ICurrencyService _currency;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ICurrencyService>(OnCurrencyReady);
    }

    private void OnDisable()
    {
        if(_currency != null)
            _currency.AmountChanged -= OnAmountChanged;
    }

    private void OnCurrencyReady(ICurrencyService currency)
    {
        _currency = currency;
        _currency.AmountChanged += OnAmountChanged;
        Refresh();
    }

    private void OnAmountChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if(_amountLabel == null || _currency == null)
            return;

        _amountLabel.text = _currency.Amount.ToString();
    }
}
