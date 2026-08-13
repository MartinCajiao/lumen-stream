using System.ComponentModel;
using System.Diagnostics;
using Lumen.Core.Client;
using Lumen.Core.Host;
using Lumen.Core.Paths;
using Lumen.Core.Quality;

namespace Lumen.Core.Runtime;

public static class SessionLauncher
{
    public static Process StartHost(StreamProfile profile, LocatedBinary binary, string? webUser = null) =>
        StartHostAsync(profile, binary, webUser, CancellationToken.None).GetAwaiter().GetResult();

    public static async Task<Process> StartHostAsync(
        StreamProfile profile,
        LocatedBinary binary,
        string? webUser,
        CancellationToken token)
    {
        Exception? last = null;
        foreach (var attempt in HostLaunchPlan.Fallbacks(profile))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var process = await TryStartOnceAsync(attempt, binary, webUser, token).ConfigureAwait(false);
                if (process is not null)
                {
                    return process;
                }
            }
            catch (InvalidOperationException ex)
            {
                last = ex;
            }

            await HostProcess.StopAllAndWaitAsync(token).ConfigureAwait(false);
        }

        var hint = HostLogHint.FromFile(LumenPaths.HostLogFile);
        throw last ?? new InvalidOperationException(
            "Apollo arrancó y se cerró. " + (hint ?? "Mira logs\\host.log. Si Windows pide permiso para el driver, acéptalo."));
    }

    public static Process StartClient(StreamProfile profile, LocatedBinary binary, string? hostAddress = null)
    {
        ClientSettingsApplier.Apply(profile);
        var args = string.IsNullOrWhiteSpace(hostAddress) ? "" : $"stream {MoonlightHost(hostAddress)} Desktop";
        var start = HiddenStart(binary.Path, args);
        start.CreateNoWindow = false;
        return Process.Start(start) ?? throw new InvalidOperationException("No se pudo arrancar el cliente.");
    }

    private static async Task<Process?> TryStartOnceAsync(
        StreamProfile profile,
        LocatedBinary binary,
        string? webUser,
        CancellationToken token)
    {
        SunshineConfigWriter.Write(profile);
        AppsJsonWriter.Write(profile);
        await HostProcess.StopAllAndWaitAsync(token).ConfigureAwait(false);
        await EnsureWebCredentialsAsync(binary, webUser, token).ConfigureAwait(false);

        var start = HiddenStart(binary.Path, Quote(LumenPaths.HostConfigFile));
        var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo arrancar el host.");
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(28);
        while (DateTime.UtcNow < until)
        {
            token.ThrowIfCancellationRequested();
            if (HostProcess.IsListening(profile.Wan.HostPort))
            {
                return HostProcess.FindRunning() ?? process;
            }

            var alive = HostProcess.IsRunning(process) || HostProcess.FindRunning() is not null;
            if (!alive)
            {
                var hint = HostLogHint.FromFile(LumenPaths.HostLogFile);
                throw new InvalidOperationException(
                    "Apollo arrancó y se cerró. " + (hint ?? "Mira logs\\host.log. Si Windows pide permiso para el driver, acéptalo."));
            }

            await Task.Delay(250, token).ConfigureAwait(false);
        }

        if (HostProcess.IsListening(profile.Wan.HostPort))
        {
            return HostProcess.FindRunning() ?? process;
        }

        return null;
    }

    private static async Task EnsureWebCredentialsAsync(
        LocatedBinary binary,
        string? webUser,
        CancellationToken token)
    {
        if (CredentialsLookOk())
        {
            return;
        }

        var user = string.IsNullOrWhiteSpace(webUser) ? "lumen" : webUser.Trim();
        var pass = user + "-host";
        var args = $"{Quote(LumenPaths.HostConfigFile)} --creds {Quote(user)} {Quote(pass)}";
        var start = HiddenStart(binary.Path, args);
        using var creds = Process.Start(start);
        if (creds is null)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await creds.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            try
            {
                if (HostProcess.IsRunning(creds))
                {
                    if (OperatingSystem.IsWindows())
                    {
                        creds.Kill(entireProcessTree: true);
                    }
                    else
                    {
                        creds.Kill();
                    }
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

            await HostProcess.StopAllAndWaitAsync(token).ConfigureAwait(false);
        }
    }

    private static bool CredentialsLookOk()
    {
        try
        {
            if (!File.Exists(LumenPaths.HostCredentialsFile))
            {
                return false;
            }

            var json = File.ReadAllText(LumenPaths.HostCredentialsFile);
            return json.Contains("username", StringComparison.OrdinalIgnoreCase)
                   && json.Contains("password", StringComparison.OrdinalIgnoreCase)
                   && json.Contains("salt", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ProcessStartInfo HiddenStart(string file, string args) => new()
    {
        FileName = file,
        Arguments = args,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        WorkingDirectory = Path.GetDirectoryName(file) ?? Environment.CurrentDirectory
    };

    private static string MoonlightHost(string address)
    {
        if (System.Net.IPAddress.TryParse(address, out var ip)
            && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            && !address.StartsWith('['))
        {
            return $"[{address}]";
        }

        return address;
    }

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
