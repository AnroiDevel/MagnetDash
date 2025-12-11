using System.Collections.Generic;
using UnityEngine;

public sealed class CollectionService : ICollectionService
{
    private const string PrefsKey = "Collection_Stars";

    private readonly HashSet<string> _collected = new HashSet<string>();

    public CollectionService()
    {
        Load();
    }

    public bool IsCollected(string starId)
    {
        return !string.IsNullOrEmpty(starId) && _collected.Contains(starId);
    }

    public void MarkCollected(string starId)
    {
        if(string.IsNullOrEmpty(starId))
            return;

        if(_collected.Add(starId))
            Save();
    }

    public IReadOnlyList<string> GetCollectedStars()
    {
        return new List<string>(_collected);
    }

    private void Load()
    {
        var raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if(string.IsNullOrEmpty(raw))
            return;

        var parts = raw.Split('|');
        foreach(var p in parts)
        {
            if(!string.IsNullOrEmpty(p))
                _collected.Add(p);
        }
    }

    private void Save()
    {
        var joined = string.Join("|", _collected);
        PlayerPrefs.SetString(PrefsKey, joined);
        PlayerPrefs.Save();
    }
}
