using Lumen.Core.Quality;
using Lumen.Core.Wan;

namespace Lumen.Core.Settings;

public sealed class LumenSettings
{
    public string Username { get; set; } = "";
    public string HostName { get; set; } = Environment.MachineName;
    public FpsPreset Fps { get; set; } = FpsPreset.Native;
    public QualityMode Quality { get; set; } = QualityMode.Game;
    public bool UseNativeResolution { get; set; } = true;
    public int ManualWidth { get; set; } = 1920;
    public int ManualHeight { get; set; } = 1080;
    public bool EnableHdr { get; set; }
    public bool PrivacyMode { get; set; } = true;
    public int MonitorCount { get; set; } = 1;
    public bool WacomPressureTilt { get; set; } = true;
    public bool AutoShareOnBoot { get; set; }
    public WanSettings Wan { get; set; } = WanSettings.ForInternet();
}
