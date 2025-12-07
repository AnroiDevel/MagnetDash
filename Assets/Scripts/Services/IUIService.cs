using System;

public interface IUIService
{
    void SetLevel(int level);
    void SetTime(float seconds);

    // UI сам достанет best time из прогресса по ключу
    void RefreshBest(int progressKey, IProgressService progress);

    void SetSpeed(float value);
    void OnPolarity(int sign); // >0 = '+', <=0 = '-'

    void ShowWinToast(float elapsedSeconds, bool isPersonalBest, int stars);
    void ShowFailToast(float elapsedSeconds);

    void SetStars(int collected, int total);


    event Action<string> ToastShown;
}
