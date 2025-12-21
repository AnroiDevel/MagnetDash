using System;
using System.Collections.Generic;

[Serializable]
public sealed class ShopSaveData
{
    public string slotId;

    public List<string> ownedSkins = new();
    public string currentSkinId = "default";
}
