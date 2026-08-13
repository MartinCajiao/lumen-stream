using System.Diagnostics;

namespace Lumen.Core.Wan;

public static class HostFirewall
{
    public static void TryAllowHost(string? exePath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return;
        }

        RunNetsh($"advfirewall firewall add rule name=\"Lumen Host\" dir=in action=allow program=\"{exePath}\" enable=yes profile=any");
        RunNetsh("advfirewall firewall add rule name=\"Lumen GameStream TCP\" dir=in action=allow protocol=TCP localport=47984-48010 profile=any");
        RunNetsh("advfirewall firewall add rule name=\"Lumen GameStream UDP\" dir=in action=allow protocol=UDP localport=47984-48010 profile=any");
    }

    private static void RunNetsh(string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(4000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
