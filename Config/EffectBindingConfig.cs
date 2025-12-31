using JFlightShaker.Enum;

public sealed class EffectBindingConfig
{
    public RumbleEffectType Effect { get; set; }
    public Guid? DeviceGuid { get; set; }
    public BindingKind Kind { get; set; }
    public string? AxisName { get; set; }
    public int? ButtonIndex { get; set; }
    public float Intensity { get; set; } = 1.0f;
}
