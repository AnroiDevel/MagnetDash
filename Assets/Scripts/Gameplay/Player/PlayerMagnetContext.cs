using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerMagnetContext
{
    public Rigidbody2D Rb;
    public PlayerMagnetConfig Config;
    public IProgressService Progress;
    public IGameEvents Events;
    public LevelManager LevelManager;

    public int Polarity = -1;

    public readonly List<MagneticNode> ActiveNodes = new();

    public MagneticNode SpawnNode;
    public MagneticNode LastVisitedNode;

    public Transform PortalTarget;

    public bool HasMagneticInfluence;
}
