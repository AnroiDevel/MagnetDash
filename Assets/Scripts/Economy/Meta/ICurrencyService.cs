public interface ICurrencyService
{
    int Amount { get; }
    void Add(int value);
    bool CanSpend(int value);
    bool Spend(int value);
}
