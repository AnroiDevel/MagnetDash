using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Music Track")]
public sealed class MusicTrack : ScriptableObject
{
    [Header("Clips")]
    [SerializeField] private AudioClip _intro;    // опционально
    [SerializeField] private AudioClip _loop;     // обязателен

    [Header("Routing & Levels")]
    [SerializeField] private AudioMixerGroup _musicBus; // группа Music
    [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

    public AudioClip Intro => _intro;
    public AudioClip Loop => _loop;
    public AudioMixerGroup MusicBus => _musicBus;
    public float Volume => _volume;
}
