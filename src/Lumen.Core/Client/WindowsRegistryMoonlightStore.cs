using System.Runtime.InteropServices;
using Lumen.Core.Client;
using Lumen.Core.Quality;
using Microsoft.Win32;

namespace Lumen.Core.Client;

public sealed class WindowsRegistryMoonlightStore : IKeyValueStore
{
    public void Set(string key, object value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var keyHandle = Registry.CurrentUser.CreateSubKey(
            $@"Software\{MoonlightSettingsWriter.Organization}\{MoonlightSettingsWriter.Application}");
        if (keyHandle is null)
        {
            return;
        }

        switch (value)
        {
            case bool b:
                keyHandle.SetValue(key, b ? 1 : 0, RegistryValueKind.DWord);
                break;
            case int i:
                keyHandle.SetValue(key, i, RegistryValueKind.DWord);
                break;
            default:
                keyHandle.SetValue(key, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                break;
        }
    }
}

public static class ClientSettingsApplier
{
    public static void Apply(StreamProfile profile)
    {
        MoonlightSettingsWriter.Apply(profile, new WindowsRegistryMoonlightStore());
        var ini = MoonlightIniPath();
        Directory.CreateDirectory(Path.GetDirectoryName(ini)!);
        File.WriteAllText(ini, MoonlightSettingsWriter.RenderIni(profile));
    }

    /// <summary>
    /// Moonlight in portable mode (portable.dat next to the exe) reads its settings from
    /// Moonlight.conf in the exe directory and ignores the registry and %AppData% entirely.
    /// </summary>
    public static string MoonlightIniPath()
    {
        var portableDir = MoonlightPairingProbe.PortableConfigDir(null);
        if (portableDir is not null)
        {
            return Path.Combine(portableDir, MoonlightPairingProbe.IniFileName);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            MoonlightSettingsWriter.Organization,
            MoonlightPairingProbe.IniFileName);
    }
}
