using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShopService : IShopService
{
    private const string OwnedKey = "Shop_OwnedSkins";
    private const string CurrentKey = "Shop_CurrentSkin";

    private readonly ICurrencyService _currency;
    private readonly ISkinDatabase _skinDatabase;
    private readonly HashSet<string> _owned = new();

    private string _currentSkinId;

    public string CurrentSkinId => _currentSkinId;

    public event Action<string> CurrentSkinChanged;

    public ShopService(ICurrencyService currency, ISkinDatabase skinDatabase, string defaultSkinId)
    {
        _currency = currency;
        _skinDatabase = skinDatabase;

        Load(defaultSkinId);
    }

    public bool IsOwned(string skinId)
    {
        return !string.IsNullOrEmpty(skinId) && _owned.Contains(skinId);
    }

    public bool TryBuy(string skinId)
    {
        if(string.IsNullOrEmpty(skinId))
            return false;

        if(IsOwned(skinId))
            return true;

        var def = _skinDatabase.GetById(skinId);
        if(def == null)
        {
            Debug.LogWarning($"ShopService: skin not found: {skinId}");
            return false;
        }

        if(!_currency.TrySpend(def.price))
            return false;

        _owned.Add(skinId);
        SaveOwned();

        return true;
    }

    public bool TrySetCurrentSkin(string skinId)
    {
        if(string.IsNullOrEmpty(skinId))
            return false;

        if(!IsOwned(skinId))
            return false;

        if(_currentSkinId == skinId)
            return true;

        _currentSkinId = skinId;
        PlayerPrefs.SetString(CurrentKey, _currentSkinId);
        PlayerPrefs.Save();

        CurrentSkinChanged?.Invoke(_currentSkinId);
        return true;
    }

    public IReadOnlyCollection<string> GetOwnedSkins()
    {
        return _owned;
    }

    private void Load(string defaultSkinId)
    {
        _owned.Clear();

        var ownedRaw = PlayerPrefs.GetString(OwnedKey, string.Empty);
        if(!string.IsNullOrEmpty(ownedRaw))
        {
            var parts = ownedRaw.Split('|');
            for(int i = 0; i < parts.Length; i++)
            {
                var id = parts[i];
                if(!string.IsNullOrEmpty(id))
                    _owned.Add(id);
            }
        }

        _currentSkinId = PlayerPrefs.GetString(CurrentKey, string.Empty);

        // первый запуск: выдаём дефолтный скин
        if(string.IsNullOrEmpty(_currentSkinId) && !string.IsNullOrEmpty(defaultSkinId))
        {
            // только если такой скин реально есть в базе
            if(_skinDatabase.GetById(defaultSkinId) != null)
            {
                _owned.Add(defaultSkinId);
                _currentSkinId = defaultSkinId;

                SaveOwned();
                PlayerPrefs.SetString(CurrentKey, _currentSkinId);
                PlayerPrefs.Save();
            }
        }
    }

    private void SaveOwned()
    {
        if(_owned.Count == 0)
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.Save();
            return;
        }

        // тут _owned небольшой, простая конкатенация норм
        var joined = string.Join("|", _owned);
        PlayerPrefs.SetString(OwnedKey, joined);
        PlayerPrefs.Save();
    }
}
