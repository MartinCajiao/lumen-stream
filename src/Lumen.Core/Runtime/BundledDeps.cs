using System.IO.Compression;
using System.Reflection;
using Lumen.Core.Paths;

namespace Lumen.Core.Runtime;

/// <summary>
/// Apollo + Moonlight can be embedded inside the launcher exe so the user never
/// installs or downloads them — Lumen extracts them to the deps dir on first
/// run. This is the "everything inside one exe, nothing bothers you" path.
/// </summary>
public static class BundledDeps
{
    /// <summary>
    /// Extracts a bundled Moonlight portable zip into %AppData%\LumenStream\deps\moonlight.
    /// Returns true if Moonlight.exe ends up present.
    /// </summary>
    public static bool ExtractMoonlight(Stream zipStream)
    {
        LumenPaths.EnsureLayout();
        var dest = Path.Combine(LumenPaths.DepsDir, "moonlight");
        try
        {
            if (Directory.Exists(dest))
            {
                Directory.Delete(dest, true);
            }

            ZipFile.ExtractToDirectory(zipStream, dest);
        }
        catch (Exception)
        {
            // partial extract is still better than nothing; locator searches nested
        }

        return ProcessLocator.FindClient() is not null;
    }

    /// <summary>
    /// Writes a bundled Apollo installer to %AppData%\LumenStream\deps and runs it
    /// silently into a sibling folder. Needs elevation (Apollo installs a driver).
    /// Returns null on success, an error message otherwise.
    /// </summary>
    public static async Task<string?> ExtractAndInstallApolloAsync(
        Stream installerStream,
        CancellationToken token)
    {
        LumenPaths.EnsureLayout();
        var installerPath = Path.Combine(LumenPaths.DepsDir, "Apollo-installer.exe");
        try
        {
            using (var file = File.Create(installerPath))
            {
                await installerStream.CopyToAsync(file, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            return "No pude escribir el instalador de Apollo: " + ex.Message;
        }

        var installDir = Path.Combine(LumenPaths.DepsDir, "Apollo");
        Directory.CreateDirectory(installDir);

        // NSIS: /S silent, /D=dir must be last and unquoted. Elevate because Apollo's
        // virtual-display driver needs admin.
        var silent = await RunAsync(installerPath, $"/S /D={installDir}", token, elevate: true).ConfigureAwait(false);
        if (silent != 0)
        {
            // Fall back to the visible wizard so the user can click Sí once.
            var visible = await RunAsync(installerPath, "", token, elevate: true).ConfigureAwait(false);
            if (visible != 0 && ProcessLocator.FindHost() is null)
            {
                return "Falló el instalador de Apollo. Acepta el permiso de Windows y reintenta.";
            }
        }

        return ProcessLocator.FindHost() is null ? "Apollo se instaló pero no encuentro sunshine.exe." : null;
    }

    /// <summary>Loads an embedded resource from the entry assembly by logical name.</summary>
    public static Stream? LoadEmbedded(string logicalName)
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var names = asm.GetManifestResourceNames();
        var match = names.FirstOrDefault(n => n.EndsWith(logicalName, StringComparison.OrdinalIgnoreCase))
                   ?? logicalName;
        return asm.GetManifestResourceStream(match);
    }

    private static async Task<int> RunAsync(string file, string args, CancellationToken token, bool elevate)
    {
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = elevate,
                CreateNoWindow = !elevate
            };
            if (elevate && OperatingSystem.IsWindows())
            {
                start.Verb = "runas";
            }

            using var process = System.Diagnostics.Process.Start(start);
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
}
