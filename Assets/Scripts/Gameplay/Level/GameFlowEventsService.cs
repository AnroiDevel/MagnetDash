using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameFlowEventsService : MonoBehaviour, IGameFlowEvents
{
    public event Action<LevelResultInfo> LevelCompleted;
    public event Action<LevelResultInfo> LevelFailed;

    private void Awake()
    {
        ServiceLocator.Register<IGameFlowEvents>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IGameFlowEvents>(this);
    }

    public void FireLevelCompleted(LevelResultInfo info)
    {
        LevelCompleted?.Invoke(info);
    }

    public void FireLevelFailed(LevelResultInfo info)
    {
        LevelFailed?.Invoke(info);
    }
}
