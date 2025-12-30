using JFlightShaker.Enum;

namespace JFlightShaker.Input;

public sealed class BindingDefinition
{
    public Guid DeviceGuid { get; init; }

    public string DeviceName { get; init; } = "";
    public BindingKind Kind { get; init; }
    public string? AxisName { get; init; }
    public int? ButtonIndex { get; init; }

    public RumbleEffectType Effect { get; init; } = RumbleEffectType.None;
    public float Intensity { get; init; } = 1.0f;
    public TriggerType Trigger { get; init; } = TriggerType.Hold;
}