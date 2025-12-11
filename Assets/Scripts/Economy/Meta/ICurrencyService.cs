public interface ICurrencyService
{
    int Amount { get; }

    void Add(int value);
    bool CanSpend(int value);
    bool TrySpend(int value);

    event System.Action<int> AmountChanged;
}
