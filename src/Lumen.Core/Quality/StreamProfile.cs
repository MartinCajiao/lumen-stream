using Lumen.Core.Display;
using Lumen.Core.Quality;
using Lumen.Core.Settings;
using Lumen.Core.Wan;

namespace Lumen.Core.Quality;

public sealed record StreamProfile
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Fps { get; init; }
    public required int PanelHz { get; init; }
    public required int BitrateKbps { get; init; }
    public required QualityMode Quality { get; init; }
    public required FpsPreset FpsPreset { get; init; }
    public bool EnableYuv444 => Quality.UsesYuv444();
    public bool EnableHdr { get; init; }
    public bool FramePacing { get; init; }
    public bool GameOptimizations { get; init; } = true;
    public bool ShowPerformanceOverlay { get; init; } = true;
    public bool PrivacyMode { get; init; }
    public int MonitorCount { get; init; } = 1;
    public bool NativePenTouch { get; init; } = true;
    public WanSettings Wan { get; init; } = WanSettings.Disabled;
    public string HostName { get; init; } = "Lumen";

    public static StreamProfile From(LumenSettings settings, DisplayInfo display)
    {
        var fps = settings.Fps.Resolve(display.RefreshHz);
        var width = settings.UseNativeResolution ? display.Width : settings.ManualWidth;
        var height = settings.UseNativeResolution ? display.Height : settings.ManualHeight;
        var bitrate = BitrateCalculator.ForProfile(width, height, fps, settings.Quality);

        return new StreamProfile
        {
            Width = width,
            Height = height,
            Fps = fps,
            PanelHz = display.RefreshHz,
            BitrateKbps = bitrate,
            Quality = settings.Quality,
            FpsPreset = settings.Fps,
            EnableHdr = settings.EnableHdr,
            FramePacing = fps >= 120,
            GameOptimizations = true,
            ShowPerformanceOverlay = true,
            PrivacyMode = settings.PrivacyMode,
            MonitorCount = Math.Clamp(settings.MonitorCount, 1, 3),
            NativePenTouch = settings.WacomPressureTilt,
            Wan = settings.Wan,
            HostName = string.IsNullOrWhiteSpace(settings.HostName) ? "Lumen" : settings.HostName.Trim()
        };
    }
}
