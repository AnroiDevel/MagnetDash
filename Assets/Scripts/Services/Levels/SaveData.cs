using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    // ===== META =====
    public string slotId;

    // ===== PROGRESS =====
    public int lastCompletedLogicalLevel = -1;

    // результаты уровней (звезды + рекорды)
    public List<LevelResultDto> levels = new();

    // факт прохождения уровней (без звёзд)
    public List<int> completedLevels = new();

    // ===== ECONOMY =====
    public int currency = 0;

    // ===== ENGINE =====
    public int engineDurability = 100;

    // ===== SHOP =====
    // ВАЖНО:
    // ShopStateService использует ТОЛЬКО эти поля
    public List<string> ownedSkins = new();
    public string currentSkinId = "";
}
