using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sfx Event")]
public sealed class SfxEvent : ScriptableObject
{
    [Header("Clips & Routing")]
    [SerializeField] private AudioClip[] _clips;
    [SerializeField] private AudioMixerGroup _output;

    [Header("Volume / Pitch")]
    [SerializeField, Range(0f, 1f)] private float _volume = 0.9f;
    [SerializeField, Range(0.5f, 2f)] private float _pitchBase = 1f;
    [SerializeField, Range(0f, 0.3f)] private float _pitchVar = 0.06f;
    [SerializeField, Range(0f, 0.3f)] private float _volVar = 0.05f;

    [Header("Spam Control")]
    [SerializeField, Range(0f, 1f)] private float _cooldown = 0.08f;
    [SerializeField, Min(0)] private int _maxSimultaneous = 4;

    public AudioMixerGroup Output => _output;
    public float Cooldown => _cooldown;
    public int MaxSimultaneous => _maxSimultaneous;

    public AudioClip GetRandomClip()
    {
        if(_clips == null || _clips.Length == 0)
            return null;

        return _clips[Random.Range(0, _clips.Length)];
    }

    public void GetRandomized(out float volume, out float pitch)
    {
        volume = Mathf.Clamp01(_volume * Random.Range(1f - _volVar, 1f + _volVar));
        pitch = Mathf.Clamp(
            _pitchBase * Random.Range(1f - _pitchVar, 1f + _pitchVar),
            0.5f, 2f
        );
    }
}
