using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public string slotId;
    public int lastCompletedLogicalLevel;
    public List<LevelResultDto> levels = new();

    public int engineDurability = 100;
    public int currency = 1000;
}
