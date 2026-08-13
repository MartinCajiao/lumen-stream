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
        SunshineConfigWriter.Write(profile);
        AppsJsonWriter.Write(profile);
        HostProcess.StopAll();
        await Task.Delay(400, token).ConfigureAwait(false);
        await EnsureWebCredentialsAsync(binary, webUser, token).ConfigureAwait(false);

        var start = HiddenStart(binary.Path, Quote(LumenPaths.HostConfigFile));
        var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo arrancar el host.");
        var up = await HostProcess.WaitUntilListeningAsync(profile.Wan.HostPort, TimeSpan.FromSeconds(20), token)
            .ConfigureAwait(false);
        if (!up)
        {
            var hint = LastLogHint();
            throw new InvalidOperationException(
                "Apollo arrancó y se cerró. " + (hint ?? "Mira logs\\host.log. Si Windows pide permiso para el driver, acéptalo."));
        }

        return HostProcess.FindRunning() ?? process;
    }

    public static Process StartClient(StreamProfile profile, LocatedBinary binary, string? hostAddress = null)
    {
        ClientSettingsApplier.Apply(profile);
        var args = string.IsNullOrWhiteSpace(hostAddress) ? "" : $"stream {MoonlightHost(hostAddress)} Desktop";
        var start = HiddenStart(binary.Path, args);
        start.CreateNoWindow = false;
        return Process.Start(start) ?? throw new InvalidOperationException("No se pudo arrancar el cliente.");
    }

    private static async Task EnsureWebCredentialsAsync(
        LocatedBinary binary,
        string? webUser,
        CancellationToken token)
    {
        if (File.Exists(LumenPaths.HostCredentialsFile))
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

        await creds.WaitForExitAsync(token).ConfigureAwait(false);
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

    private static string? LastLogHint()
    {
        try
        {
            if (!File.Exists(LumenPaths.HostLogFile))
            {
                return null;
            }

            var lines = File.ReadLines(LumenPaths.HostLogFile).Reverse().Take(8).Reverse();
            var text = string.Join(' ', lines);
            return text.Length > 240 ? text[^240..] : text;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
