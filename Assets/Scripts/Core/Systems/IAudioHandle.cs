public interface IAudioHandle
{
    bool IsValid { get; }
    void SetVolume(float v, float fadeSeconds = 0f);
    void Stop(float fadeSeconds = 0.1f);
}
