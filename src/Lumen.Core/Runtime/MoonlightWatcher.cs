using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lumen.Core.Runtime;

/// <summary>
/// Watches the Moonlight client process and its windows to report back to Lumen
/// whether pairing is still in progress, streaming is active, or Moonlight has
/// closed. This makes Lumen feel synchronized with Moonlight in real time.
/// </summary>
public static class MoonlightWatcher
{
    public static MoonlightState Inspect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return MoonlightState.Unknown;
        }

        var processes = Process.GetProcessesByName("Moonlight");
        if (processes.Length == 0)
        {
            return MoonlightState.Closed;
        }

        foreach (var process in processes)
        {
            try
            {
                var hWnd = process.MainWindowHandle;
                if (hWnd == IntPtr.Zero)
                {
                    // Process exists but main window not ready yet (starting/pairing).
                    return MoonlightState.PairingOrIdle;
                }

                if (!IsWindowVisible(hWnd))
                {
                    // Window is hidden (pairing in progress, Lumen hid it).
                    return MoonlightState.PairingOrIdle;
                }

                // Window is visible. Check if it looks like the stream (big window).
                if (IsFullscreenOrBig(hWnd))
                {
                    return MoonlightState.Streaming;
                }

                return MoonlightState.PairingOrIdle;
            }
            catch (Exception)
            {
                // Ignore stale processes.
            }
        }

        return MoonlightState.PairingOrIdle;
    }

    private static bool IsFullscreenOrBig(IntPtr hWnd)
    {
        try
        {
            _ = GetWindowRect(hWnd, out var rect);
            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;
            // Consider it a stream if it's bigger than 1280x720 or fullscreen.
            return w >= 1280 && h >= 720;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    private readonly record struct Rect(int Left, int Top, int Right, int Bottom);
}

public enum MoonlightState
{
    Unknown,
    Closed,
    PairingOrIdle,
    Streaming
}
