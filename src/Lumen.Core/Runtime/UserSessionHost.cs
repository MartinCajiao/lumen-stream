using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lumen.Core.Runtime;

/// <summary>
/// Host must run in the user session (not Session 0) so capture works.
/// This is "on boot" in the Parsec sense: when you log into Windows.
/// </summary>
public static class UserSessionHost
{
    public const string RunValueName = "LumenHost";

    public static string CommandLine(string exePath) => $"\"{exePath}\" --host";

    public static void SetEnabled(bool enabled, string exePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(RunValueName, CommandLine(exePath));
        }
        else
        {
            key.DeleteValue(RunValueName, false);
        }
    }

    public static bool IsEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue(RunValueName) is string;
    }
}
