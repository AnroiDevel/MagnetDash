using System;

public interface IProgressService
{
    int GetStars(int buildIndex);                     // 0..3
    bool SetStarsMax(int buildIndex, int stars);     // true, если улучшили
    bool TryGetBestTime(int buildIndex, out float t);
    bool SetBestTimeIfBetter(int buildIndex, float t);
    void ResetAll();

    bool TryGetLastCompletedLevel(out int buildIndex);


    event Action<int, int> StarsChanged;              // (buildIndex, newStars)
    event Action<int, float> BestTimeChanged;         // (buildIndex, newTime)
}
