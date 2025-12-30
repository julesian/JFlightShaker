using ThrottleShaker.Config;

public sealed class ActiveProfile
{
    public AppConfig AppConfig { get; init; } = new();
    public ThrottleSettings ThrottleSettings { get; init; } = new();
    public GunHoldSettings GunSettings { get; init; } = new();
    public List<BindingConfig> Bindings { get; set; } = new();
}
