using System.Collections.Generic;

public sealed class SkinDatabaseService : ISkinDatabase
{
    private readonly Dictionary<string, DroneSkinDefinition> _map;
    private readonly DroneSkinDefinition[] _all;

    public SkinDatabaseService(SkinDatabase source)
    {
        if(source == null || source.skins == null)
        {
            _all = new DroneSkinDefinition[0];
            _map = new Dictionary<string, DroneSkinDefinition>();
            return;
        }

        _all = source.skins;
        _map = new Dictionary<string, DroneSkinDefinition>(_all.Length);

        foreach(var skin in _all)
        {
            if(skin == null || string.IsNullOrEmpty(skin.id))
                continue;

            _map[skin.id] = skin;
        }
    }

    public DroneSkinDefinition GetById(string id)
    {
        if(string.IsNullOrEmpty(id))
            return null;

        return _map.TryGetValue(id, out var skin) ? skin : null;
    }

    public DroneSkinDefinition[] GetAll()
    {
        return _all;
    }
}
