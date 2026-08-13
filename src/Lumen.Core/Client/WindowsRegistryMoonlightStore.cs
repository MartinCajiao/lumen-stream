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
        var iniDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            MoonlightSettingsWriter.Organization);
        Directory.CreateDirectory(iniDir);
        File.WriteAllText(Path.Combine(iniDir, "Moonlight.conf"), MoonlightSettingsWriter.RenderIni(profile));
    }
}
