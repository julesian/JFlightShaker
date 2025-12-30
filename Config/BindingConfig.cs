namespace JFlightShaker.Config;
using JFlightShaker.Enum;

public sealed class BindingConfig
{
    public Guid DeviceGuid { get; set; }
    public string? DeviceName { get; set; }

    public BindingKind Kind { get; set; }

    // Axis
    public string? AxisName { get; set; }

    // Button
    public int? ButtonIndex { get; set; }

    public RumbleEffectType Effect { get; set; }
    public float Intensity { get; set; } = 1f;
}
