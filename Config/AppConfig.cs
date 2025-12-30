namespace ThrottleShaker.Config;

public sealed class AppConfig
{
    public String Version { get; set; } = "0.0.1";

    // Devices
    public string? SelectedAudioDeviceId { get; set; }
    public Guid? SelectedThrottleDeviceGuid { get; set; }
    public Guid? SelectedStickDeviceGuid { get; set; }

    // Configs
    public string ThrottleProfilePath { get; set; } = @"profiles\throttle_effect.json";
    public string GunProfilePath { get; set; } = @"profiles\gun_effect.json";

    public string BindingsPath { get; set; } = "bindings.json";
}
