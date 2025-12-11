using System.Collections.Generic;

public interface ICollectionService
{
    bool IsCollected(string starId);
    void MarkCollected(string starId);
    IReadOnlyList<string> GetCollectedStars();
}
