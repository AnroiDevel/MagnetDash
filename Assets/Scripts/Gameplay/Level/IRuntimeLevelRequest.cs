public interface IRuntimeLevelRequest
{
    bool HasRequest { get; }
    int RequestedIndex { get; }
    void Clear();
    void Set(int index);
}
