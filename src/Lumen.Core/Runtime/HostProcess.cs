using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Lumen.Core.Runtime;

public static class HostProcess
{
    public static bool IsRunning(Process? process)
    {
        if (process is null)
        {
            return false;
        }

        try
        {
            return !process.HasExited;
        }
        catch (Win32Exception)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static Process? FindRunning()
    {
        foreach (var name in new[] { "sunshine", "apollo" })
        {
            Process[] found;
            try
            {
                found = Process.GetProcessesByName(name);
            }
            catch (Win32Exception)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var process in found)
            {
                if (IsRunning(process))
                {
                    return process;
                }

                process.Dispose();
            }
        }

        return null;
    }

    public static bool IsLive()
    {
        try
        {
            return IsListening() || FindRunning() is not null;
        }
        catch (Win32Exception)
        {
            return IsListening();
        }
        catch (UnauthorizedAccessException)
        {
            return IsListening();
        }
    }

    public static bool IsListening(int port = 47989)
    {
        try
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = 400;
            client.SendTimeout = 400;
            var task = client.ConnectAsync(IPAddress.Loopback, port);
            return task.Wait(TimeSpan.FromMilliseconds(400)) && client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    public static void SweepZombies()
    {
        if (FindRunning() is not null && !IsListening())
        {
            StopAll();
        }
    }

    public static async Task StopAllAndWaitAsync(CancellationToken token)
    {
        StopAll();
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        while (DateTime.UtcNow < until)
        {
            token.ThrowIfCancellationRequested();
            if (FindRunning() is null && !IsListening())
            {
                return;
            }

            StopAll();
            await Task.Delay(300, token).ConfigureAwait(false);
        }
    }

    public static void StopAll()
    {
        if (OperatingSystem.IsWindows())
        {
            RunTaskKill("sunshine.exe");
            RunTaskKill("apollo.exe");
        }

        foreach (var name in new[] { "sunshine", "apollo" })
        {
            Process[] found;
            try
            {
                found = Process.GetProcessesByName(name);
            }
            catch (Win32Exception)
            {
                continue;
            }

            foreach (var process in found)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    else
                    {
                        process.Kill();
                    }
                }
                catch (Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static void RunTaskKill(string image)
    {
        try
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /IM {image} /T",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            kill?.WaitForExit(5000);
        }
        catch (Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public static async Task<bool> WaitUntilListeningAsync(int port, TimeSpan timeout, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            token.ThrowIfCancellationRequested();
            if (IsListening(port))
            {
                return true;
            }

            await Task.Delay(250, token).ConfigureAwait(false);
        }

        return IsListening(port);
    }
}
