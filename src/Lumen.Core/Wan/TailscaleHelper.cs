using System.Diagnostics;
using System.Net.Http;

namespace Lumen.Core.Wan;

/// <summary>
/// Tailscale is the honest fix for CGNAT/double NAT: a free overlay network.
/// Lumen detects it, offers a one-click installer, and prefers the 100.x address.
/// </summary>
public static class TailscaleHelper
{
    public const string InstallerUrl = "https://pkgs.tailscale.com/stable/tailscale-setup-latest-amd64.msi";

    public static bool IsConnected => !string.IsNullOrWhiteSpace(NetworkAddresses.TailscaleIpv4());

    public static string? FindBinary()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tailscale", "tailscale.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailscale", "tailscale.exe")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static bool IsInstalled => FindBinary() is not null || IsConnected;

    /// <summary>Downloads the official MSI and opens it (UAC prompt included). Returns the temp file path.</summary>
    public static async Task<string> DownloadAndLaunchInstallerAsync(
        IProgress<string>? progress,
        CancellationToken token)
    {
        var target = Path.Combine(Path.GetTempPath(), "tailscale-setup-lumen.msi");
        progress?.Report("Bajando Tailscale (gratis)…");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        await using (var input = await http.GetStreamAsync(InstallerUrl, token).ConfigureAwait(false))
        await using (var output = File.Create(target))
        {
            await input.CopyToAsync(output, token).ConfigureAwait(false);
        }

        progress?.Report("Abriendo el instalador de Tailscale. Acepta el permiso de Windows…");
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
        return target;
    }
}
