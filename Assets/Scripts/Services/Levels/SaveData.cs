using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int version = 1;
    public string slotId = "default";
    public List<LevelResultDto> levels = new();
    public int lastCompletedLogicalLevel = -1; // -1 = нет прогресса
}
