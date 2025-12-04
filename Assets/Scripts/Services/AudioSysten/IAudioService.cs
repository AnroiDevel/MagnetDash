using UnityEngine;

public interface IAudioService
{
    float MasterVolume01 { get; set; }
    float MusicVolume01 { get; set; }
    float SfxVolume01 { get; set; }
    float UIVolume01 { get; set; }
    bool Muted {  get; set; }


    void Play(SfxEvent sfx, string key = null);

    void PlayAt(
        SfxEvent sfx,
        Vector3 worldPos,
        float spatial = 1f,
        float minDist = 2f,
        float maxDist = 20f,
        string key = null
    );

    IAudioHandle PlayLoop(
        SfxEvent sfx,
        Vector3 worldPos,
        float spatial = 1f,
        float minDist = 2f,
        float maxDist = 20f
    );
}
