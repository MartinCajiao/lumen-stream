using System.Runtime.InteropServices;
using Avalonia;
using Lumen.Core.Paths;
using Lumen.Core.Runtime;

namespace Lumen.Launcher;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (args.Any(a => a.Equals("--host", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = HostDaemon.Run();
                return;
            }

            if (args.Any(a => a.Equals("--install-deps", StringComparison.OrdinalIgnoreCase)))
            {
                var error = DependencyInstaller.EnsureAsync().GetAwaiter().GetResult();
                Environment.ExitCode = error is null && DependencyInstaller.IsReady() ? 0 : 1;
                if (error is not null)
                {
                    Console.Error.WriteLine(error);
                }

                return;
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            TryLog(ex);
            ShowCrash(ex.GetBaseException().Message);
            Environment.ExitCode = 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void TryLog(Exception ex)
    {
        try
        {
            LumenPaths.EnsureLayout();
            File.WriteAllText(Path.Combine(LumenPaths.Root, "logs", "crash.log"), ex.ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ShowCrash(string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        MessageBoxW(IntPtr.Zero, "Lumen no pudo abrir.\n\n" + message, "Lumen", 0x10);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
