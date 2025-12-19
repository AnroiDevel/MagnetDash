using System;
using UnityEngine;

public interface IPlayerRegistry
{
    event Action<Transform> PlayerSpawned;
    Transform Current { get; }
    void Register(Transform player);
}
