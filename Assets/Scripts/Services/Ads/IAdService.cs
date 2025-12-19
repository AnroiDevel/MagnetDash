using System;

public interface IAdService
{
    bool IsAvailable { get; }
    void ShowRewarded(Action onSuccess, Action onFail = null);
}
