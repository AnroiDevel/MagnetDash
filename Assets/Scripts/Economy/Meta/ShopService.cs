using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShopService : IShopService
{
    private readonly ICurrencyService _currency;
    private readonly ISkinDatabase _db;
    private readonly IShopState _state;

    public string CurrentSkinId => _state.CurrentSkinId;

    public event Action<string> CurrentSkinChanged
    {
        add => _state.CurrentSkinChanged += value;
        remove => _state.CurrentSkinChanged -= value;
    }

    public ShopService(ICurrencyService currency, ISkinDatabase skinDatabase, IShopState state, string defaultSkinId)
    {
        _currency = currency;
        _db = skinDatabase;
        _state = state;

        _state.Initialize(defaultSkinId, _db);
    }

    public bool IsOwned(string skinId) => _state.IsOwned(skinId);

    public IReadOnlyCollection<string> GetOwnedSkins() => _state.GetOwnedSkins();

    public bool TryBuy(string skinId)
    {
        skinId = skinId?.Trim();
        if(string.IsNullOrEmpty(skinId))
            return false;

        if(_state.IsOwned(skinId))
            return true;

        var def = _db.GetById(skinId);
        if(def == null)
        {
            Debug.LogWarning($"[ShopService] Skin not found: '{skinId}'.");
            return false;
        }

        if(_currency == null)
        {
            Debug.LogError("[ShopService] CurrencyService is null.");
            return false;
        }

        if(!_currency.TrySpend(def.price))
            return false;

        // фиксируем покупку
        if(_state.AddOwned(skinId))
            return true;

        // rollback: деньги списали, а стейт не принял -> возвращаем
        _currency.Add(def.price);
        Debug.LogError($"[ShopService] Purchase failed after spending. skinId='{skinId}' price={def.price}");
        return false;
    }

    public bool TrySetCurrentSkin(string skinId) => _state.TrySetCurrent(skinId);
}
