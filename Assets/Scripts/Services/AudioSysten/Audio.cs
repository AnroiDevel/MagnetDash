using UnityEngine;

public static class Audio
{
    public static IAudioService Service { get; private set; }
    public static void Initialize(IAudioService impl) => Service = impl;

    public static void Play(SfxEvent sfx, string key = null) =>
        Service?.Play(sfx, key);

    public static void PlayAt(SfxEvent sfx, Vector3 pos, float spatial = 1f, float minDist = 2f, float maxDist = 20f, string key = null) =>
        Service?.PlayAt(sfx, pos, spatial, minDist, maxDist, key);

    public static IAudioHandle PlayLoop(SfxEvent sfx, Vector3 pos, float spatial = 1f, float minDist = 2f, float maxDist = 20f) =>
        Service?.PlayLoop(sfx, pos, spatial, minDist, maxDist);
}
