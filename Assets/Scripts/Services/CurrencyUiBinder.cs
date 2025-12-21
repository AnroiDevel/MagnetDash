using UnityEngine;

[DisallowMultipleComponent]
public sealed class CurrencyUiBinder : MonoBehaviour
{
    private ICurrencyService _currency;
    private IUIService _ui;

    private void OnEnable()
    {
        ServiceLocator.WhenAvailable<ICurrencyService>(BindCurrency);
        ServiceLocator.WhenAvailable<IUIService>(BindUi);
    }

    private void OnDisable()
    {
        ServiceLocator.Unsubscribe<ICurrencyService>(BindCurrency);
        ServiceLocator.Unsubscribe<IUIService>(BindUi);

        UnbindCurrency();
        _ui = null;
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

    private void BindUi(IUIService ui)
    {
        _ui = ui;
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
        if(_currency == null || _ui == null)
            return;

        //_ui.SetCurrency(_currency.Amount); // метод должен существовать в IUIService
    }
}
