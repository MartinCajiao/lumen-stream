using Lumen.Core.Quality;

namespace Lumen.Core.Overlay;

public sealed record StreamStats(
    int RequestedFps,
    int PanelHz,
    int? CaptureHz,
    int? DecodeFps,
    string? Warning)
{
    public string OverlayLine
    {
        get
        {
            var capture = CaptureHz is int c ? $"{c}" : "?";
            var decode = DecodeFps is int d ? $"{d}" : "?";
            return $"captura {capture}  decode {decode}  panel {PanelHz} Hz  pedido {RequestedFps}";
        }
    }
}

public static class OverlayAdvisor
{
    public static string? Warn(int requestedFps, int panelHz, int width, int height)
    {
        if (panelHz > 0 && requestedFps > panelHz)
        {
            return $"Tu pantalla es {panelHz} Hz. Pediste {requestedFps}: el cliente no puede sentir más de {panelHz} Hz.";
        }

        if (width * height >= 3840 * 2160 && requestedFps >= 120)
        {
            return "4K a 120+ Hz necesita LAN gorda y NVENC/AMF potente. Si se ve mal, baja resolución, no Hz.";
        }

        return null;
    }

    public static StreamStats Compose(StreamProfile profile, int? captureHz, int? decodeFps) =>
        new(
            profile.Fps,
            profile.PanelHz,
            captureHz,
            decodeFps,
            Warn(profile.Fps, profile.PanelHz, profile.Width, profile.Height));
}

public static class HostLogStatsParser
{
    public static int? TryCaptureHz(string logChunk)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            logChunk,
            @"Display refresh rate \[(\d+(?:\.\d+)?)Hz\]");
        if (!match.Success)
        {
            return null;
        }

        return (int)Math.Round(double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    public static int? TryRequestedFps(string logChunk)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            logChunk,
            @"Requested frame rate \[(\d+)fps\]");
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }
}
