using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sfx Event")]
public sealed class SfxEvent : ScriptableObject
{
    public AudioClip[] clips;
    public AudioMixerGroup output;
    [Range(0f, 1f)] public float volume = 0.9f;
    [Range(0.5f, 2f)] public float pitchBase = 1f;
    [Range(0f, 0.3f)] public float pitchVar = 0.06f;
    [Range(0f, 0.3f)] public float volVar = 0.05f;
    [Range(0f, 1f)] public float cooldown = 0.08f;
    [Min(0)] public int maxSimultaneous = 4;

    public AudioClip RandomClip() =>
        (clips == null || clips.Length == 0) ? null : clips[Random.Range(0, clips.Length)];

    public void Randomize(ref float vol, ref float pitch)
    {
        vol = Mathf.Clamp01(volume * Random.Range(1f - volVar, 1f + volVar));
        pitch = Mathf.Clamp(pitchBase * Random.Range(1f - pitchVar, 1f + pitchVar), 0.5f, 2f);
    }
}
