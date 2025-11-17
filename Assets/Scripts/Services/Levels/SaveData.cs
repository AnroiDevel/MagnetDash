// SaveData.cs
using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int version = 1;
    public string slotId = "default";
    public List<LevelResultDto> levels = new(); // JsonUtility не любит Dictionary
    public bool hintsEnabled = true;            // пример глобальной настройки
}
