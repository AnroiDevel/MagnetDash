using UnityEngine;

public sealed class LevelMusicStarter : MonoBehaviour
{
    [SerializeField] private MusicManager _music;
    [SerializeField] private MusicTrack _track;
    [SerializeField] private float _fade = 0.6f;

    private void Start()
    {
        _music.Play(_track, _fade);
    }
}
