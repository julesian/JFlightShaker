using JFlightShaker.Config;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace JFlightShaker.Audio;

public sealed class RumbleEngine : IDisposable
{
    private WasapiOut? _out;
    private ThrottleRumbleProvider? _provider;

    public bool IsRunning => _out != null;

    public bool Enabled
    {
        get => _provider?.Enabled ?? false;
        set { if (_provider != null) _provider.Enabled = value; }
    }

    public void Start(MMDevice device, ThrottleSettings s)
    {
        Stop();

        _provider = new ThrottleRumbleProvider(s.SampleRate, s.Channels, s);
        _provider.Enabled = true;

        _out = new WasapiOut(device, AudioClientShareMode.Shared, true, 30);
        _out.Init(_provider);
        _out.Play();
    }

    public void SetTargetAmplitude(float amp01)
        => _provider?.SetTargetAmplitude(Math.Clamp(amp01, 0f, 1f));

    public void Stop()
    {
        if (_provider != null) _provider.Enabled = false;

        _out?.Stop();
        _out?.Dispose();
        _out = null;

        _provider = null;
    }

    public void Dispose() => Stop();
}
