namespace ThrottleShaker.Config;

public sealed class RumbleProfile
{
    public ThrottleSettings Throttle { get; set; } = new();
    public GunHoldSettings Gun { get; set; } = new();
}