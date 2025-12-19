using System;
using System.Collections.Generic;

public interface IShopService
{
    string CurrentSkinId { get; }

    event Action<string> CurrentSkinChanged;

    bool IsOwned(string skinId);
    bool TryBuy(string skinId);
    bool TrySetCurrentSkin(string skinId);

    IReadOnlyCollection<string> GetOwnedSkins();
}
