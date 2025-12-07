// LevelResultDto.cs
using System;

[Serializable]
public struct LevelResultDto
{
    public int levelId;
    public int stars;           // 0..3
    public bool hasBestTime;    // JsonUtility не умеет nullable
    public float bestTime;      // валидно только при hasBestTime==true

}
