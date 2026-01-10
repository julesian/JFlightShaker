namespace JFlightShaker.Config;
using JFlightShaker.Enum;

public sealed class BindingConfig
{
    public Guid? DeviceGuid { get; set; }

    public string? DeviceName { get; set; }

    public BindingKind Kind { get; set; }

    // Axis
    // TODO: Axis object, maybe?
    public string? AxisName { get; set; }
    public float? AxisMin { get; set; }
    public float? AxisMax { get; set; }
    public bool InvertAxis { get; set; }


    // Button
    public int? ButtonIndex { get; set; }
    public TriggerType Trigger { get; set; } = TriggerType.Hold;

    // Effect
    public RumbleEffectType Effect { get; set; }
    public float Intensity { get; set; } = 1f;


}
