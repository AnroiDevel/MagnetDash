using UnityEngine;

public sealed class LevelIndexResolver
{
    private readonly int _firstLevelSceneIndex;
    private readonly int _runtimeLevelSceneIndex;
    private readonly int _totalScenesInBuild;

    private int _manualCountCached = -1;

    public LevelIndexResolver(int firstLevelSceneIndex, int runtimeLevelSceneIndex, int totalScenesInBuild)
    {
        _firstLevelSceneIndex = firstLevelSceneIndex;
        _runtimeLevelSceneIndex = runtimeLevelSceneIndex;
        _totalScenesInBuild = totalScenesInBuild;
    }

    public int GetManualLevelsCount()
    {
        if(_manualCountCached >= 0)
            return _manualCountCached;

        if(IsValidBuildIndex(_runtimeLevelSceneIndex) && _runtimeLevelSceneIndex > _firstLevelSceneIndex)
            _manualCountCached = Mathf.Max(0, _runtimeLevelSceneIndex - _firstLevelSceneIndex);
        else
            _manualCountCached = Mathf.Max(0, _totalScenesInBuild - _firstLevelSceneIndex);

        return _manualCountCached;
    }

    public int GetLevelNumberForUI(int sceneBuildIndex, bool isRuntimeLevel, int currentRuntimeJsonIndex)
    {
        if(isRuntimeLevel && currentRuntimeJsonIndex >= 0)
            return GetManualLevelsCount() + currentRuntimeJsonIndex + 1;

        return sceneBuildIndex - _firstLevelSceneIndex + 1;
    }

    /// <summary>
    /// 0..∞ логический индекс:
    /// 0..manualCount-1  – ручные уровни
    /// manualCount..     – runtime JSON уровни
    /// </summary>
    public int GetLogicalLevelIndex(int sceneBuildIndex, bool isRuntimeLevel, int currentRuntimeJsonIndex)
    {
        if(isRuntimeLevel && currentRuntimeJsonIndex >= 0)
            return GetManualLevelsCount() + currentRuntimeJsonIndex;

        return sceneBuildIndex - _firstLevelSceneIndex;
    }

    /// <summary>
    /// Ключ для прогресса:
    /// - ручные уровни: buildIndex
    /// - JSON уровни: отрицательные id (0 -> -1, 1 -> -2, ...)
    /// </summary>
    public int GetProgressKey(int sceneBuildIndex, bool isRuntimeLevel, int currentRuntimeJsonIndex)
    {
        if(isRuntimeLevel && currentRuntimeJsonIndex >= 0)
            return -1 - currentRuntimeJsonIndex;

        return sceneBuildIndex;
    }

    public bool TryResolveLogicalIndex(int logicalIndex, out LevelTarget target)
    {
        if(logicalIndex < 0)
        {
            target = default;
            return false;
        }

        int manualCount = GetManualLevelsCount();

        if(logicalIndex < manualCount)
        {
            target = LevelTarget.Manual(buildIndex: _firstLevelSceneIndex + logicalIndex);
            return true;
        }

        target = LevelTarget.Runtime(jsonIndex: logicalIndex - manualCount);
        return true;
    }

    private bool IsValidBuildIndex(int idx) => idx >= 0 && idx < _totalScenesInBuild;

    public readonly struct LevelTarget
    {
        public readonly bool isRuntime;
        public readonly int buildIndex;
        public readonly int jsonIndex;

        private LevelTarget(bool isRuntime, int buildIndex, int jsonIndex)
        {
            this.isRuntime = isRuntime;
            this.buildIndex = buildIndex;
            this.jsonIndex = jsonIndex;
        }

        public static LevelTarget Manual(int buildIndex) => new LevelTarget(false, buildIndex, -1);
        public static LevelTarget Runtime(int jsonIndex) => new LevelTarget(true, -1, jsonIndex);
    }
}
