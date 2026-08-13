using System.Diagnostics;
using Lumen.Core.Runtime;

namespace Lumen.Setup;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Lumen Setup");
        Console.WriteLine("Instala Apollo + Moonlight y deja el host listo para el arranque.");
        Console.WriteLine();

        var progress = new Progress<InstallProgress>(p => Console.WriteLine("  " + p.Message));
        var error = await DependencyInstaller.EnsureAsync(progress).ConfigureAwait(false);
        if (error is not null)
        {
            Console.WriteLine(error);
            return 1;
        }

        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "Lumen Stream");
        Directory.CreateDirectory(startMenu);

        var lumen = LocateLumen();
        if (lumen is not null)
        {
            var autoShare = args.Any(a => a.Equals("--autoshare", StringComparison.OrdinalIgnoreCase));
            UserSessionHost.SetEnabled(autoShare, lumen);
            Console.WriteLine(autoShare
                ? "Al iniciar sesión, este PC se comparte solo."
                : "Host en segundo plano: actívalo en la app (opcional).");
        }

        Console.WriteLine("Listo. Abre Lumen, entra a tu cuenta y verás tus PCs.");
        return DependencyInstaller.IsReady() ? 0 : 1;
    }

    private static string? LocateLumen()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "Lumen.exe");
        if (File.Exists(beside))
        {
            return beside;
        }

        try
        {
            var found = Process.GetProcessesByName("Lumen").FirstOrDefault()?.MainModule?.FileName;
            return File.Exists(found) ? found : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
