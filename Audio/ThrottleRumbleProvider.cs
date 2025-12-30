using NAudio.Wave;
using ThrottleShaker.Config;

namespace ThrottleShaker.Audio;

public sealed class ThrottleRumbleProvider : WaveProvider32
{
    private readonly ThrottleSettings _s;

    private float _amp;
    private volatile float _ampTarget;

    private double _phase;
    private readonly Random _rng = new(1);

    // simple 1st-order high-pass state
    private float _hpXPrev;
    private float _hpYPrev;

    public bool Enabled { get; set; } = true;

    public ThrottleRumbleProvider(int sampleRate, int channels, ThrottleSettings settings)
        : base(sampleRate, channels)
    {
        _s = settings;
    }

    public void SetTargetAmplitude(float a) => _ampTarget = a;

    public override int Read(float[] buffer, int offset, int sampleCount)
    {
        float target = Enabled ? _ampTarget : 0f;
        int ch = WaveFormat.Channels;
        int sr = WaveFormat.SampleRate;

        // Base rumble freq
        const float baseHz = 45f;

        // High-pass around ~10 Hz to strip DC (RC filter)
        const float hpCut = 10f;
        float rc = 1f / (2f * (float)Math.PI * hpCut);
        float dt = 1f / sr;
        float alpha = rc / (rc + dt);

        for (int i = 0; i < sampleCount; i += ch)
        {
            _amp += (target - _amp) * _s.AmpSmoothing;

            // sine
            _phase += (2.0 * Math.PI * baseHz) / sr;
            if (_phase > Math.PI * 2.0) _phase -= Math.PI * 2.0;
            float s = (float)Math.Sin(_phase);

            // noise in [-1, 1]
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0f);

            // mix
            float raw = (0.8f * s) + (0.2f * noise);

            // amplitude envelope
            float sig = raw * _amp;

            // high-pass
            float y = alpha * (_hpYPrev + sig - _hpXPrev);
            _hpXPrev = sig;
            _hpYPrev = y;

            buffer[offset + i] = y;
            if (ch > 1)
                buffer[offset + i + 1] = y;
        }

        return sampleCount;
    }
}
