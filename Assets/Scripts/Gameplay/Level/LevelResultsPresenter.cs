using UnityEngine;

public sealed class LevelResultsPresenter
{
    private readonly IProgressService _progress;
    private readonly IGameFlowEvents _flowEvents;
    private readonly int _starsPerLevel;
    private readonly int _engineRepairThreshold;

    public LevelResultsPresenter(
        IProgressService progress,
        IGameFlowEvents flowEvents,
        int starsPerLevel,
        int engineRepairThreshold)
    {
        _progress = progress;
        _flowEvents = flowEvents;
        _starsPerLevel = starsPerLevel;
        _engineRepairThreshold = engineRepairThreshold;
    }

    public void ShowWin(
        int buildIndex,
        int levelNumber,
        int progressKey,
        int logicalIndex,
        LevelSession session)
    {
        float elapsed = session?.Elapsed ?? 0f;
        bool pb = _progress?.SetBestTimeIfBetter(progressKey, elapsed) ?? false;

        int collected = session?.CollectedStars ?? 0;
        int starsPerLevel = session?.StarsPerLevel ?? _starsPerLevel;
        int clampedStars = Mathf.Clamp(collected, 0, starsPerLevel);

        if(clampedStars > 0)
            _progress?.SetStarsMax(progressKey, clampedStars);

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        _flowEvents?.FireLevelCompleted(new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNumber,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = clampedStars,
            isPersonalBest = pb,
            isWin = true,
            hint = "Используй обе полярности!"
        });

        if(logicalIndex >= 0)
            _progress?.SetLastCompletedLevelIfHigher(logicalIndex);

        TryShowEngineRepairUI();
    }

    public void ShowFail(
        int buildIndex,
        int levelNumber,
        int progressKey,
        LevelSession session)
    {
        float elapsed = session?.Elapsed ?? 0f;

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        _flowEvents?.FireLevelFailed(new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNumber,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = session?.CollectedStars ?? 0,
            isPersonalBest = false,
            isWin = false,
            hint = "Попробуй другую траекторию!"
        });
    }

    private void TryShowEngineRepairUI()
    {
        if(_progress == null || !_progress.IsLoaded)
            return;

        int power = (int)(_progress.EnginePower * 100);
        if(power > _engineRepairThreshold)
            return;

        int powerPercent = Mathf.RoundToInt(_progress.EnginePower * 100f);

        if(ServiceLocator.TryGet<IUIService>(out var ui))
            ui.ShowEngineRepairOffer(powerPercent);
    }
}
