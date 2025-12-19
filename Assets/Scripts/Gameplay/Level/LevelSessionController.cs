public sealed class LevelSessionController
{
    private readonly LevelSession _session;

    public LevelSessionController(int starsPerLevel)
    {
        _session = new LevelSession(starsPerLevel);
    }

    public LevelSession Session => _session;

    public void StartLevel(int buildIndex) => _session.StartLevel(buildIndex);
    public void CollectStar() => _session.CollectStar();
}
