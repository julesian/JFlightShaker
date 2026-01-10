using JFlightShaker.Enum;

namespace JFlightShaker.Config;

public static class EffectBindingRules
{
    public static IReadOnlyList<BindingKind> GetAllowedKinds(RumbleEffectType effect)
    {
        return effect switch
        {
            RumbleEffectType.ThrottleAxis => new[] { BindingKind.Axis },
            RumbleEffectType.Gun => new[] { BindingKind.Button },
            RumbleEffectType.MuteEffects => new[] { BindingKind.Button },
            _ => new[] { BindingKind.Axis }
        };
    }

    public static bool IsKindAllowed(RumbleEffectType effect, BindingKind kind)
        => GetAllowedKinds(effect).Contains(kind);

    public static IReadOnlyList<TriggerType> GetAllowedTriggers(RumbleEffectType effect)
    {
        return effect switch
        {
            RumbleEffectType.Gun => new[] { TriggerType.Hold },
            RumbleEffectType.MuteEffects => new[] { TriggerType.Hold, TriggerType.Press },
            _ => new[] { TriggerType.Hold }
        };
    }

    public static bool IsTriggerAllowed(RumbleEffectType effect, TriggerType trigger)
        => GetAllowedTriggers(effect).Contains(trigger);
}