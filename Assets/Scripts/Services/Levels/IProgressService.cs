using System;

public interface IProgressService
{
    bool IsLoaded { get; }                    // данные загружены и готовы
    int EngineDurability { get; }
    float EnginePower { get; }

    int GetStars(int buildIndex);
    bool SetStarsMax(int buildIndex, int stars);
    bool TryGetBestTime(int buildIndex, out float t);
    bool SetBestTimeIfBetter(int buildIndex, float t);

    void ResetAll();

    bool TryGetLastCompletedLevel(out int logicalIndex);
    void SetLastCompletedLevelIfHigher(int logicalIndex);
    void DamageEngine(int amount);
    void RepairEngineFull();

    event Action Loaded;                      // данные загрузились (локально/облако)
    event Action<int, int> StarsChanged;      // (buildIndex, newStars)
    event Action<int, float> BestTimeChanged; // (buildIndex, newTime)
    event Action<int> EngineDurabilityChanged; // 0..100

}
