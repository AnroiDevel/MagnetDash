using System;

[Serializable]
public sealed class LevelResultInfo
{
    public int levelBuildIndex;
    public int levelNumber;
    public float elapsedTime;
    public float? bestTime;
    public int collectedStars;
    public bool isPersonalBest;
    public bool isWin;
    public string hint;

    public LevelRewardInfo reward;
}
