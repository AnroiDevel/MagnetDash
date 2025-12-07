using System;

public interface IProgressService
{
    bool IsLoaded { get; }                    // данные загружены и готовы

    int GetStars(int buildIndex);
    bool SetStarsMax(int buildIndex, int stars);
    bool TryGetBestTime(int buildIndex, out float t);
    bool SetBestTimeIfBetter(int buildIndex, float t);
    void ResetAll();

    bool TryGetLastCompletedLevel(out int logicalIndex);
    void SetLastCompletedLevelIfHigher(int logicalIndex);

    event Action Loaded;                      // данные загрузились (локально/облако)
    event Action<int, int> StarsChanged;      // (buildIndex, newStars)
    event Action<int, float> BestTimeChanged; // (buildIndex, newTime)
}
