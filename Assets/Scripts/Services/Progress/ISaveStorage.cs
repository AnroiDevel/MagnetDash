using System;

public interface ISaveStorage
{
    bool IsAvailable { get; }
    void Load(string slotId, Action<string> onOk, Action<string> onFail);
    void Save(string slotId, string json);
}
