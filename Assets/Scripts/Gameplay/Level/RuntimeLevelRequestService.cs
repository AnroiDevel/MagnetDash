public sealed class RuntimeLevelRequestService : IRuntimeLevelRequest
{
    public bool HasRequest { get; private set; }
    public int RequestedIndex { get; private set; }

    public void Set(int index)
    {
        RequestedIndex = index;
        HasRequest = true;
    }

    public void Clear()
    {
        HasRequest = false;
    }
}
