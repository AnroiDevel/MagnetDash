using System;

[Serializable]
public sealed class LevelRewardInfo
{
    public int baseReward;

    public int firstClear;
    public int starsDelta;
    public int timeRecord;

    // runtime-only (не сохраняем)
    public bool doubled;
    public int doubleBonus;

    public int Total => baseReward + doubleBonus;
    public bool CanDoubleNow => baseReward > 0 && !doubled;
}
