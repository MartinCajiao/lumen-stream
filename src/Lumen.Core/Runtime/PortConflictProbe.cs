using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Lumen.Core.Runtime;

/// <summary>
/// Explains why the GameStream HTTP port cannot be bound before Lumen wastes
/// relaunch cycles on an Apollo that dies immediately.
/// </summary>
public static partial class PortConflictProbe
{
    public static string? Diagnose(int port)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var pid = FindListeningPid(port);
        if (pid is null || pid.Value == Environment.ProcessId)
        {
            return null;
        }

        string name;
        try
        {
            name = Process.GetProcessById(pid.Value).ProcessName;
        }
        catch (Exception)
        {
            name = $"proceso {pid.Value}";
        }

        if (name.Contains("sunshine", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("apollo", StringComparison.OrdinalIgnoreCase))
        {
            return $"El puerto {port} ya lo está usando {name} (PID {pid.Value}). Es otro Apollo o Sunshine: ciérralo o detén su servicio y vuelve a compartir. Apollo no puede abrir el mismo puerto dos veces.";
        }

        return $"El puerto {port} ya lo usa {name} (PID {pid.Value}). Cierra ese programa y vuelve a compartir.";
    }

    public static int? FindListeningPid(int port)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano -p tcp",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);
            foreach (var line in output.Split('\n'))
            {
                if (TryParseListeningLine(line, port, out var pid))
                {
                    return pid;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    /// <summary>Parses a netstat line like "TCP 0.0.0.0:47989 0.0.0.0:0 LISTENING 1234".</summary>
    public static bool TryParseListeningLine(string line, int port, out int pid)
    {
        pid = 0;
        var match = ListeningLine().Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups["port"].Value != port.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            return false;
        }

        return int.TryParse(match.Groups["pid"].Value, out pid) && pid > 0;
    }

    [GeneratedRegex(@"^\s*TCP\s+\S*:(?<port>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningLine();
}