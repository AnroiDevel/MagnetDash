using UnityEngine;

public sealed class LevelResultsPresenter
{
    private readonly IProgressService _progress;
    private readonly IGameFlowEvents _flowEvents;
    private readonly int _starsPerLevel;
    private readonly int _engineRepairThreshold;
    private readonly EconomyConfig _economy;

    public LevelResultsPresenter(
        IProgressService progress,
        IGameFlowEvents flowEvents,
        int starsPerLevel,
        int engineRepairThreshold,
        EconomyConfig economy)
    {
        _progress = progress;
        _flowEvents = flowEvents;
        _starsPerLevel = starsPerLevel;
        _engineRepairThreshold = engineRepairThreshold;
        _economy = economy;
    }

    public void ShowWin(int buildIndex, int levelNumber, int progressKey, int logicalIndex, LevelSession session)
    {
        int prevStars = _progress != null ? _progress.GetStars(progressKey) : 0;

        bool firstClear = false;
        if(_progress is ILevelCompletionService completion)
            firstClear = completion.MarkCompleted(progressKey);

        int collected = session?.CollectedStars ?? 0;
        int starsPerLevel = session?.StarsPerLevel ?? _starsPerLevel;
        int clampedStars = Mathf.Clamp(collected, 0, starsPerLevel);

        if(clampedStars > 0)
            _progress?.SetStarsMax(progressKey, clampedStars);

        int deltaStars = Mathf.Max(0, clampedStars - prevStars);

        float elapsed = session?.Elapsed ?? 0f;
        bool pb = _progress?.SetBestTimeIfBetter(progressKey, elapsed) ?? false;

        float? best = null;
        if(_progress != null && _progress.TryGetBestTime(progressKey, out var bestTime))
            best = bestTime;

        int firstClearReward = 0;
        int starsReward = 0;
        int timeReward = 0;

        if(_economy != null)
        {
            if(firstClear)
                firstClearReward = _economy.GetFirstClearReward(logicalIndex);

            starsReward = deltaStars * _economy.starDeltaReward;

            if(!firstClear && pb)
                timeReward = _economy.timeRecordReward;
        }

        int reward = firstClearReward + starsReward + timeReward;

        if(reward > 0 && _progress is ICurrencyService currency)
            currency.Add(reward);

        LevelRewardInfo rewardInfo = null;
        if(reward > 0)
        {
            rewardInfo = new LevelRewardInfo
            {
                baseReward = reward,
                firstClear = firstClearReward,
                starsDelta = starsReward,
                timeRecord = timeReward,
                doubled = false,
                doubleBonus = 0
            };
        }

        _flowEvents?.FireLevelCompleted(new LevelResultInfo
        {
            levelBuildIndex = buildIndex,
            levelNumber = levelNumber,
            elapsedTime = elapsed,
            bestTime = best,
            collectedStars = clampedStars,
            isPersonalBest = pb,
            isWin = true,
            hint = "Используй обе полярности!",
            reward = rewardInfo
        });

        if(logicalIndex >= 0)
            _progress?.SetLastCompletedLevelIfHigher(logicalIndex);

        TryShowEngineRepairUI();
    }

    public void ShowFail(int buildIndex, int levelNumber, int progressKey, LevelSession session)
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
            hint = "Попробуй другую траекторию!",
            reward = null
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
