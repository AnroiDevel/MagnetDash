using UnityEngine;

public sealed class LevelSession
{
    private readonly int _starsPerLevel;

    private int _currentLevelBuildIndex;
    private int _collectedStars;
    private float _levelStartTime;

    public LevelSession(int starsPerLevel)
    {
        _starsPerLevel = Mathf.Max(0, starsPerLevel);
    }

    public void StartLevel(int buildIndex)
    {
        _currentLevelBuildIndex = buildIndex;
        _collectedStars = 0;
        _levelStartTime = Time.time;
    }

    public int CurrentLevelBuildIndex => _currentLevelBuildIndex;
    public int StarsPerLevel => _starsPerLevel;
    public int CollectedStars => _collectedStars;
    public float Elapsed => Time.time - _levelStartTime;

    public void CollectStar()
    {
        if(_collectedStars < _starsPerLevel)
            _collectedStars++;
    }
}
