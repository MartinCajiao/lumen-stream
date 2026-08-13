using System.Text.Json;
using Lumen.Core.Settings;
using Lumen.Core.Wan;

namespace Lumen.Core.Paths;

public static class LumenPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumenStream");

    public static string SettingsFile => Path.Combine(Root, "lumen.json");
    public static string AccountsFile => Path.Combine(Root, "accounts.json");
    public static string HostConfigDir => Path.Combine(Root, "host");
    public static string HostConfigFile => Path.Combine(HostConfigDir, "sunshine.conf");
    public static string HostAppsFile => Path.Combine(HostConfigDir, "apps.json");
    public static string HostStateFile => Path.Combine(HostConfigDir, "sunshine_state.json");
    public static string HostCredentialsFile => Path.Combine(HostConfigDir, "credentials.json");
    public static string HostLogFile => Path.Combine(Root, "logs", "host.log");
    public static string WanFile => Path.Combine(Root, "wan.json");
    public static string DisplaysFile => Path.Combine(Root, "lumen-displays.json");
    public static string ComputersFile => Path.Combine(Root, "computers.json");
    public static string DepsDir => Path.Combine(Root, "deps");

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(HostConfigDir);
        Directory.CreateDirectory(DepsDir);
        Directory.CreateDirectory(Path.GetDirectoryName(HostLogFile)!);
    }
}

public static class LumenSettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static LumenSettings Load()
    {
        LumenPaths.EnsureLayout();
        if (!File.Exists(LumenPaths.SettingsFile))
        {
            return new LumenSettings();
        }

        var json = File.ReadAllText(LumenPaths.SettingsFile);
        var settings = JsonSerializer.Deserialize<LumenSettings>(json, Json) ?? new LumenSettings();
        if (!settings.Wan.EnableUpnp && !settings.Wan.EnableStun)
        {
            settings.Wan = WanSettings.ForInternet(string.IsNullOrWhiteSpace(settings.Wan.ExternalIp) ? null : settings.Wan.ExternalIp);
        }

        return settings;
    }

    public static void Save(LumenSettings settings)
    {
        LumenPaths.EnsureLayout();
        File.WriteAllText(LumenPaths.SettingsFile, JsonSerializer.Serialize(settings, Json));
    }
}
