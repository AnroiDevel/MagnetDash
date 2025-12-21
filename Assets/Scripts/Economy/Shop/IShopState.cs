using System;
using System.Collections.Generic;

public interface IShopState
{
    string CurrentSkinId { get; }
    event Action<string> CurrentSkinChanged;

    bool IsOwned(string skinId);
    IReadOnlyCollection<string> GetOwnedSkins();

    bool AddOwned(string skinId);         // добавляет, если не было
    bool TrySetCurrent(string skinId);    // меняет current, если owned
    void Initialize(string defaultSkinId, ISkinDatabase db);
}
