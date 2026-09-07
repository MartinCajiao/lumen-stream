using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lumen.Core.Runtime;

/// <summary>
/// Small Win32 helper to hide or show Moonlight windows. When Lumen starts
/// Moonlight only for pairing, we hide it so the user only interacts with Lumen.
/// When the stream starts, we show it again so the user sees the game/desktop.
/// </summary>
public static class WindowHelper
{
    private const int SwHide = 0;
    private const int SwShowNormal = 1;

    public static void HideMoonlightPairWindow(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        // Moonlight can take a moment to create its main window. Retry briefly.
        for (var i = 0; i < 20; i++)
        {
            var hWnd = process.MainWindowHandle;
            if (hWnd != IntPtr.Zero && IsWindowVisible(hWnd))
            {
                _ = ShowWindow(hWnd, SwHide);
                return;
            }

            Thread.Sleep(100);
        }
    }

    public static void ShowMoonlight(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        var hWnd = process.MainWindowHandle;
        if (hWnd != IntPtr.Zero)
        {
            _ = ShowWindow(hWnd, SwShowNormal);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
