using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using Lumen.Core.Paths;

namespace Lumen.Core.Runtime;

public sealed record InstallProgress(string Message, bool Done = false, string? Error = null);

public static class GitHubAssetPicker
{
    public static string? PickInstaller(IEnumerable<string> names, string contains)
    {
        var list = names.ToList();
        return list.FirstOrDefault(n =>
                   n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                   && n.Contains(contains, StringComparison.OrdinalIgnoreCase)
                   && n.Contains("Setup", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(n =>
                   n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                   && n.Contains(contains, StringComparison.OrdinalIgnoreCase)
                   && !n.Contains("debug", StringComparison.OrdinalIgnoreCase)
                   && !n.Contains("symbols", StringComparison.OrdinalIgnoreCase)
                   && !n.Contains("AppImage", StringComparison.OrdinalIgnoreCase));
    }

    public static string? PickPortableZip(IEnumerable<string> names, string contains) =>
        names.FirstOrDefault(n =>
            n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            && n.Contains(contains, StringComparison.OrdinalIgnoreCase)
            && n.Contains("x64", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Installs Apollo + Moonlight once. Moonlight is portable (no admin).
/// Apollo's NSIS installer needs UAC; if silent fails we open the wizard.
/// </summary>
public static class DependencyInstaller
{
    public static bool HasHost() => ProcessLocator.FindHost() is not null;

    public static bool HasClient() => ProcessLocator.FindClient() is not null;

    public static bool IsReady() => HasHost() && HasClient();

    public static async Task<string?> EnsureAsync(
        IProgress<InstallProgress>? progress = null,
        CancellationToken token = default,
        bool needHost = true,
        bool needClient = true)
    {
        bool HostOk() => !needHost || HasHost();
        bool ClientOk() => !needClient || HasClient();

        if (HostOk() && ClientOk())
        {
            progress?.Report(new("Listo.", true));
            return null;
        }

        LumenPaths.EnsureLayout();

        if (needHost && !HasHost())
        {
            // Try the Apollo installer embedded in the launcher first — no download.
            var bundledApollo = BundledDeps.LoadEmbedded("ApolloInstaller");
            if (bundledApollo is not null)
            {
                progress?.Report(new("Preparando Apollo (ya incluido en Lumen)…"));
                var apollo = await BundledDeps.ExtractAndInstallApolloAsync(bundledApollo, token).ConfigureAwait(false);
                if (apollo is not null && !HasHost())
                {
                    return apollo;
                }
            }
            else
            {
                progress?.Report(new("Descargando Apollo…"));
                var apollo = await InstallApolloAsync(token, progress).ConfigureAwait(false);
                if (apollo is not null && !HasHost())
                {
                    return apollo;
                }
            }
        }

        if (needClient && !HasClient())
        {
            // Try Moonlight embedded in the launcher first — no download.
            var bundledMoonlight = BundledDeps.LoadEmbedded("MoonlightPortable");
            if (bundledMoonlight is not null)
            {
                progress?.Report(new("Preparando Moonlight (ya incluido en Lumen)…"));
                using (bundledMoonlight)
                {
                    BundledDeps.ExtractMoonlight(bundledMoonlight);
                }
                if (!HasClient())
                {
                    return "Moonlight se extrajo pero no aparece Moonlight.exe.";
                }
            }
            else
            {
                progress?.Report(new("Descargando Moonlight…"));
                var moonlight = await InstallMoonlightPortableAsync(token, progress).ConfigureAwait(false);
                if (moonlight is not null && !HasClient())
                {
                    return moonlight;
                }
            }
        }

        if (HostOk() && ClientOk())
        {
            progress?.Report(new("Listo.", true));
            return null;
        }

        return "Falta Apollo o Moonlight. Acepta el permiso de Windows si lo pide.";
    }

    private static async Task<string?> InstallMoonlightPortableAsync(
        CancellationToken token,
        IProgress<InstallProgress>? progress)
    {
        progress?.Report(new("Descargando Moonlight portable…"));
        var asset = await FindAssetAsync("moonlight-stream", "moonlight-qt", zip: true, "MoonlightPortable", token).ConfigureAwait(false);
        if (asset is null)
        {
            asset = await FindAssetAsync("moonlight-stream", "moonlight-qt", zip: false, "MoonlightSetup", token).ConfigureAwait(false);
            if (asset is null)
            {
                return "No encontré Moonlight para Windows.";
            }

            return await DownloadAndRunInstallerAsync(asset.Value.Url, asset.Value.Name, token, progress).ConfigureAwait(false);
        }

        var zipPath = Path.Combine(LumenPaths.DepsDir, asset.Value.Name);
        if (!await DownloadFileAsync(asset.Value.Url, zipPath, token, progress).ConfigureAwait(false))
        {
            return "No pude descargar Moonlight.";
        }

        var dest = Path.Combine(LumenPaths.DepsDir, "moonlight");
        try
        {
            if (Directory.Exists(dest))
            {
                Directory.Delete(dest, true);
            }

            ZipFile.ExtractToDirectory(zipPath, dest);
        }
        catch (Exception ex)
        {
            return "No pude descomprimir Moonlight: " + ex.Message;
        }

        return ProcessLocator.FindClient() is null ? "Moonlight se descargó pero no aparece Moonlight.exe." : null;
    }

    private static async Task<string?> InstallApolloAsync(
        CancellationToken token,
        IProgress<InstallProgress>? progress)
    {
        progress?.Report(new("Descargando Apollo…"));
        var asset = await FindAssetAsync("ClassicOldSong", "Apollo", zip: false, "Apollo", token).ConfigureAwait(false);
        if (asset is null)
        {
            return "No encontré el instalador de Apollo.";
        }

        return await DownloadAndRunInstallerAsync(asset.Value.Url, asset.Value.Name, token, progress).ConfigureAwait(false);
    }

    private static async Task<string?> DownloadAndRunInstallerAsync(
        string url,
        string name,
        CancellationToken token,
        IProgress<InstallProgress>? progress)
    {
        var dest = Path.Combine(LumenPaths.DepsDir, name);
        if (!await DownloadFileAsync(url, dest, token, progress).ConfigureAwait(false))
        {
            return $"No pude descargar {name}.";
        }

        var installDir = Path.Combine(LumenPaths.DepsDir, Path.GetFileNameWithoutExtension(name));
        Directory.CreateDirectory(installDir);

        progress?.Report(new($"Instalando {name}. Si Windows pide permiso, pulsa Sí…"));
        // NSIS: /S silent, /D=dir must be last and unquoted.
        var silent = await RunAsync(dest, $"/S /D={installDir}", token, elevate: true).ConfigureAwait(false);
        if (silent != 0)
        {
            progress?.Report(new("El modo silencioso falló. Abro el instalador para que pulses Siguiente…"));
            var visible = await RunAsync(dest, "", token, elevate: true).ConfigureAwait(false);
            if (visible != 0 && ProcessLocator.FindHost() is null && ProcessLocator.FindClient() is null)
            {
                return $"Falló el instalador {name}. Cierra el antivirus un momento y reintenta, o ejecuta el archivo en %AppData%\\LumenStream\\deps.";
            }
        }

        return ProcessLocator.FindHost() is null && name.Contains("Apollo", StringComparison.OrdinalIgnoreCase)
            ? "Apollo se descargó pero no encuentro sunshine.exe. Acepta el permiso de Windows y reintenta."
            : null;
    }

    private static async Task<(string Name, string Url)?> FindAssetAsync(
        string owner,
        string repo,
        bool zip,
        string hint,
        CancellationToken token)
    {
        using var http = CreateHttp();
        using var response = await http.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var n = asset.GetProperty("name").GetString() ?? "";
            var picked = zip
                ? GitHubAssetPicker.PickPortableZip([n], hint)
                : GitHubAssetPicker.PickInstaller([n], hint);
            if (picked is null)
            {
                continue;
            }

            var url = asset.GetProperty("browser_download_url").GetString();
            if (url is not null)
            {
                return (n, url);
            }
        }

        return null;
    }

    private static async Task<bool> DownloadFileAsync(string url, string dest, CancellationToken token, IProgress<InstallProgress>? progress)
    {
        try
        {
            using var http = CreateHttp();
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using var output = File.Create(dest);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            progress?.Report(new($"Descargado {Path.GetFileName(dest)}."));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<int> RunAsync(string file, string args, CancellationToken token, bool elevate)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = elevate,
                CreateNoWindow = !elevate
            };
            if (elevate && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                start.Verb = "runas";
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return -1;
            }

            await process.WaitForExitAsync(token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LumenStream", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }
}
