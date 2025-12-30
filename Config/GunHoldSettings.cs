public sealed class GunHoldSettings
{
    public float PulseHz { get; set; } = 18f;
    public float Punch { get; set; } = 0.65f;
    public float Jitter { get; set; } = 0.12f;
    public float Floor { get; set; } = 0.03f;

    // Default bindings
    public int DefaultButtonIndex { get; set; } = 0;
}
