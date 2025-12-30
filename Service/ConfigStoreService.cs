using System.IO;
using System.Text.Json;
using JFlightShaker.Config;

namespace JFlightShaker.Service;

public sealed class ConfigStoreService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string RootDir { get; }
    public string AppConfigPath => Path.Combine(RootDir, "appsettings.json");

    public ConfigStoreService(string appName = "JFlightShaker")
    {
        RootDir = Path.Combine(AppContext.BaseDirectory, "Config");
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(Path.Combine(RootDir, "profiles"));
    }

    public AppConfig LoadAppConfig()
    {
        if (!File.Exists(AppConfigPath))
        {
            var cfg = new AppConfig();
            SaveAppConfig(cfg);
            return cfg;
        }

        var json = File.ReadAllText(AppConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
    }

    public void SaveAppConfig(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, JsonOpts);
        File.WriteAllText(AppConfigPath, json);
    }

    public T LoadProfile<T>(string relativePath) where T : new()
    {
        var full = Path.Combine(RootDir, relativePath);

        if (!File.Exists(full))
        {
            var def = new T();
            SaveProfile(relativePath, def);
            return def;
        }

        var json = File.ReadAllText(full);
        return JsonSerializer.Deserialize<T>(json, JsonOpts) ?? new T();
    }

    public void SaveProfile<T>(string relativePath, T profile)
    {
        var full = Path.Combine(RootDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        var json = JsonSerializer.Serialize(profile, JsonOpts);
        File.WriteAllText(full, json);
    }

    public List<BindingConfig> LoadBindings(string relativePath)
    {
        return LoadProfile<List<BindingConfig>>(relativePath);
    }

    public void SaveBindings(string relativePath, List<BindingConfig> bindings)
    {
        SaveProfile(relativePath, bindings);
    }
}
