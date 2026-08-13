using Lumen.Core.Display;
using Lumen.Core.Host;
using Lumen.Core.Quality;
using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class SunshineConfigWriterTests
{
    private static StreamProfile Profile(bool privacy, int monitors = 1) =>
        StreamProfile.From(new LumenSettings
        {
            Fps = FpsPreset.Hz144,
            Quality = QualityMode.SharpLan,
            PrivacyMode = privacy,
            MonitorCount = monitors,
            WacomPressureTilt = true,
            UseNativeResolution = false,
            ManualWidth = 1920,
            ManualHeight = 1080,
            HostName = "LumenTest"
        }, new DisplayInfo(1920, 1080, 60, "TEST"));

    [Fact]
    public void Privacy_mode_uses_ensure_only_display()
    {
        var conf = SunshineConfigWriter.Render(Profile(privacy: true));
        Assert.Contains("dd_configuration_option = ensure_only_display", conf);
        Assert.Contains("isolated_virtual_display_option = enabled", conf);
        Assert.DoesNotContain("ensure_primary", conf);
        Assert.Contains("upnp = enabled", conf);
        Assert.Contains("credentials_file", conf);
        Assert.Contains("pkey =", conf);
        Assert.Contains("ignore_encoder_probe_failure = enabled", conf);
    }

    [Fact]
    public void Without_privacy_virtual_display_is_primary()
    {
        var conf = SunshineConfigWriter.Render(Profile(privacy: false));
        Assert.Contains("dd_configuration_option = ensure_primary", conf);
    }

    [Fact]
    public void Advertises_high_refresh_and_matches_client_hz()
    {
        var conf = SunshineConfigWriter.Render(Profile(privacy: true));
        Assert.Contains("144", conf);
        Assert.Contains("165", conf);
        Assert.Contains("240", conf);
        Assert.Contains("dd_refresh_rate_option = auto", conf);
        Assert.Contains("dd_resolution_option = auto", conf);
        Assert.Contains("native_pen_touch = enabled", conf);
        Assert.Contains("hevc_mode = 3", conf);
    }

    [Fact]
    public void Apps_json_creates_one_virtual_desktop_per_monitor()
    {
        var json = AppsJsonWriter.Render(Profile(privacy: true, monitors: 3));
        Assert.Contains("\"virtual-display\": true", json);
        Assert.Contains("Desktop 1", json);
        Assert.Contains("Desktop 3", json);
    }
}
