using Lumen.Core.Quality;
using Xunit;

namespace Lumen.Tests;

public sealed class BitrateCalculatorTests
{
    [Fact]
    public void Higher_fps_needs_more_bitrate_than_60()
    {
        var at60 = BitrateCalculator.MoonlightDefaultKbps(1920, 1080, 60, false);
        var at144 = BitrateCalculator.MoonlightDefaultKbps(1920, 1080, 144, false);
        Assert.True(at144 > at60);
    }

    [Fact]
    public void Yuv444_doubles_moonlight_budget()
    {
        var yuv420 = BitrateCalculator.MoonlightDefaultKbps(1920, 1080, 60, false);
        var yuv444 = BitrateCalculator.MoonlightDefaultKbps(1920, 1080, 60, true);
        Assert.Equal(yuv420 * 2, yuv444);
    }

    [Fact]
    public void Sharp_lan_1080p_is_between_80_and_150_mbps()
    {
        var kbps = BitrateCalculator.ForProfile(1920, 1080, 144, QualityMode.SharpLan);
        Assert.InRange(kbps, BitrateCalculator.SharpLanMinKbps, BitrateCalculator.SharpLanMaxKbps);
    }

    [Fact]
    public void Four_k_144_is_capped_at_150_mbps()
    {
        var kbps = BitrateCalculator.ForProfile(3840, 2160, 144, QualityMode.SharpLan);
        Assert.Equal(BitrateCalculator.SharpLanMaxKbps, kbps);
    }
}
