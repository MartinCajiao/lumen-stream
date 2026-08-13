using Lumen.Core.Client;
using Lumen.Core.Display;
using Lumen.Core.Quality;
using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class MoonlightSettingsWriterTests
{
    [Fact]
    public void Writes_144_fps_444_overlay_and_frame_pacing()
    {
        var profile = StreamProfile.From(new LumenSettings
        {
            Fps = FpsPreset.Hz144,
            Quality = QualityMode.SharpLan,
            UseNativeResolution = false,
            ManualWidth = 2560,
            ManualHeight = 1440
        }, new DisplayInfo(2560, 1440, 144, "TEST"));

        var store = new MemoryKeyValueStore();
        MoonlightSettingsWriter.Apply(profile, store);

        Assert.Equal(144, store.Values["fps"]);
        Assert.Equal(true, store.Values["yuv444"]);
        Assert.Equal(true, store.Values["showperfoverlay"]);
        Assert.Equal(true, store.Values["framepacing"]);
        Assert.Equal(true, store.Values["gameopts"]);
        Assert.Equal(2, store.Values["videocfg"]);
        Assert.True((int)store.Values["bitrate"] >= 80_000);
    }

    [Fact]
    public void Native_preset_follows_panel_hz()
    {
        var profile = StreamProfile.From(new LumenSettings { Fps = FpsPreset.Native }, new DisplayInfo(1920, 1080, 165, "TEST"));
        Assert.Equal(165, profile.Fps);
        Assert.Equal(165, profile.PanelHz);
    }
}
