using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LevelJsonRoot
{
    public List<LevelJsonData> levels;
}

[Serializable]
public sealed class LevelJsonData
{
    public int id;
    public Vec2 player;
    public Vec2 portal;
    public List<NodeJsonData> nodes;

    // 3 звезды для сбора
    public List<Vec2> starsToCollect;
}


[Serializable]
public sealed class NodeJsonData
{
    public Vec2 position;
    public bool isPositive;
}

[Serializable]
public sealed class Vec2
{
    public float x;
    public float y;

    public Vec2() { }

    public Vec2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2 ToVector2() => new(x, y);
}
