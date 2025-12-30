namespace JFlightShaker.Config;

public sealed class ThrottleSettings
{
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;

    // Axis → amplitude mapping
    public float Deadzone { get; set; } = 0.05f;
    public float BaselineAmp { get; set; } = 0.08f;
    public float TopAmp { get; set; } = 0.16f;
    public float AmpSmoothing { get; set; } = 0.90f;
    public bool InvertAxis { get; set; } = false;
    public float ResponseCurve { get; set; } = 1.8f;

    // Default bindings
    public string DefaultAxisName { get; set; } = "RotationX";
}
