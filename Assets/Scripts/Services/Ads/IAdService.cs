using System;

public interface IAdService
{
    bool IsAvailable { get; }
    bool IsRewardedReady { get; }

    void PreloadRewarded();
    void ShowRewarded(Action onSuccess, Action onFail = null);
}
