namespace JFlightShaker.Audio;

public interface RumbleEffect
{
    bool Finished { get; }
    float UpdateAndGetAmp(float dtSeconds); // 0..1
    void Stop();
}
