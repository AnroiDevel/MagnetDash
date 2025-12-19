using System;
using UnityEngine;

public sealed class PlayerRegistry : IPlayerRegistry
{
    public event Action<Transform> PlayerSpawned;

    public Transform Current { get; private set; }

    public void Register(Transform player)
    {
        if(player == null)
            return;

        Current = player;
        PlayerSpawned?.Invoke(player);
    }
}
