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
            _ => new[] { BindingKind.Axis }
        };
    }

    public static bool IsKindAllowed(RumbleEffectType effect, BindingKind kind)
        => GetAllowedKinds(effect).Contains(kind);
}