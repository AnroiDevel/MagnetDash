using UnityEngine;

[CreateAssetMenu(menuName = "MagnetDash/Economy Config", fileName = "EconomyConfig")]
public sealed class EconomyConfig : ScriptableObject
{
    [Header("First Clear Reward")]
    public int firstClearBase = 40;
    public int firstClearStep = 2;
    public int firstClearCap = 30;

    [Header("Stars")]
    public int starDeltaReward = 25;

    [Header("Time Record")]
    public int timeRecordReward = 10;

    public int GetFirstClearReward(int logicalIndex)
    {
        int idx = Mathf.Max(0, logicalIndex);
        return firstClearBase + Mathf.Min(idx, firstClearCap) * firstClearStep;
    }
}
