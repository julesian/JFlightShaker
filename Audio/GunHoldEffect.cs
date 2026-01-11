namespace JFlightShaker.Audio;

public sealed class GunHoldEffect : RumbleEffect
{
    private readonly Random _rng = new();
    private float _t;
    private bool _stopped;

    public bool Finished => _stopped;

    public float Intensity { get; }
    public GunHoldSettings Settings { get; }

    public GunHoldEffect(float intensity, GunHoldSettings? settings = null)
    {
        Intensity = Math.Clamp(intensity, 0f, 1f);
        Settings = settings ?? new GunHoldSettings();
    }

    public float UpdateAndGetAmp(float dtSeconds)
    {
        if (_stopped) return 0f;

        if (dtSeconds < 0f) dtSeconds = 0f;
        if (dtSeconds > 0.1f) dtSeconds = 0.1f;

        _t += dtSeconds;

        float hz = Math.Max(0.1f, Settings.PulseHz);
        float sine = (float)(0.5 + 0.5 * Math.Sin(_t * 2.0 * Math.PI * hz)); // 0..1

        // Punch shaping: lerp between sine and sine^2
        float punch = Math.Clamp(Settings.Punch, 0f, 1f);
        float shaped = Lerp(sine, sine * sine, punch);

        // Optional jitter
        float jitter = Math.Clamp(Settings.Jitter, 0f, 1f);
        if (jitter > 0f)
        {
            float r = (float)(_rng.NextDouble() * 2.0 - 1.0); // -1..1
            shaped = Math.Clamp(shaped + r * jitter * 0.15f, 0f, 1f);
        }

        // Optional floor gate
        float floor = Math.Clamp(Settings.Floor, 0f, 1f);
        if (shaped < floor) shaped = 0f;

        return Math.Clamp(Intensity * shaped, 0f, 1f);
    }

    public void Stop() => _stopped = true;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
