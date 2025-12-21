using System;
using System.IO;
using UnityEngine;

public sealed class LocalFileStorage : ISaveStorage
{
    private readonly string _dir;
    private readonly string _prefix;

    public bool IsAvailable => true;

    public LocalFileStorage(string dir, string prefix)
    {
        _dir = dir;
        _prefix = string.IsNullOrEmpty(prefix) ? "save_" : prefix;
        Directory.CreateDirectory(_dir);
    }

    public void Load(string slotId, Action<string> onOk, Action<string> onFail)
    {
        try
        {
            var path = MakePath(slotId);
            var json = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            onOk?.Invoke(json ?? string.Empty);
        }
        catch(Exception e)
        {
            onFail?.Invoke($"local_load_error:{e.GetType().Name}");
        }
    }

    public void Save(string slotId, string json)
    {
        try
        {
            var path = MakePath(slotId);
            File.WriteAllText(path, json ?? string.Empty);
        }
        catch(Exception e)
        {
            Debug.LogError($"[LocalFileStorage] Save error: {e}");
        }
    }

    private string MakePath(string slotId)
    {
        slotId = string.IsNullOrEmpty(slotId) ? "default" : slotId.Trim();
        return Path.Combine(_dir, $"{_prefix}{slotId}.json");
    }
}
