using System;

public interface IGameFlowEvents
{
    event Action<LevelResultInfo> LevelCompleted;
    event Action<LevelResultInfo> LevelFailed;

    void FireLevelCompleted(LevelResultInfo info);
    void FireLevelFailed(LevelResultInfo info);
}
