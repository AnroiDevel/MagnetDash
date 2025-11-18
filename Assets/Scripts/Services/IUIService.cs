using System;

public interface IUIService
{
    void SetLevel(int level);
    void SetTime(float seconds);
    void RefreshBest(float? bestSeconds, IProgressService progress);
    void SetSpeed(float value);
    void OnPolarity(int sign); // >0 = '+', <=0 = '-'

    void ShowWinToast(float elapsedSeconds, bool isPersonalBest, int stars);
    void ShowFailToast();

    // ѕо желанию можно слушать изменени€ состо€ни€ UI
    event Action<string> ToastShown;
}
