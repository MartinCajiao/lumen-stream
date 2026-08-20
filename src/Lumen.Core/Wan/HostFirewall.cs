using System.Diagnostics;
using Lumen.Core.Discovery;

namespace Lumen.Core.Wan;

public static class HostFirewall
{
    /// <summary>
    /// Opens the ports Lumen needs. Returns false when the rules could not be applied
    /// (usually because the process is not elevated), so the caller can warn the user.
    /// </summary>
    public static bool TryAllowHost(string? hostExe)
    {
        var ok = true;
        if (OperatingSystem.IsWindows())
        {
            if (!string.IsNullOrWhiteSpace(hostExe) && File.Exists(hostExe))
            {
                ok &= RunNetsh($"advfirewall firewall add rule name=\"Lumen Host\" dir=in action=allow program=\"{hostExe}\" enable=yes profile=any");
            }

            ok &= RunNetsh("advfirewall firewall add rule name=\"Lumen GameStream TCP\" dir=in action=allow protocol=TCP localport=47984-48010 profile=any");
            ok &= RunNetsh("advfirewall firewall add rule name=\"Lumen GameStream UDP\" dir=in action=allow protocol=UDP localport=47984-48010 profile=any");
        }

        var launcher = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(launcher) && File.Exists(launcher))
        {
            ok &= RunNetsh($"advfirewall firewall add rule name=\"Lumen Launcher\" dir=in action=allow program=\"{launcher}\" enable=yes profile=any");
            ok &= RunNetsh($"advfirewall firewall add rule name=\"Lumen Beacon\" dir=in action=allow protocol=UDP localport={LanBeacon.Port} profile=any");
        }

        return ok;
    }

    private static bool RunNetsh(string args)
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
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(4000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}