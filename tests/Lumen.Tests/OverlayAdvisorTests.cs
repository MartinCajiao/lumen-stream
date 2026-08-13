using Lumen.Core.Overlay;
using Lumen.Core.Quality;
using Xunit;

namespace Lumen.Tests;

public sealed class OverlayAdvisorTests
{
    [Fact]
    public void Warns_when_client_panel_is_slower_than_request()
    {
        var warning = OverlayAdvisor.Warn(144, 60, 1920, 1080);
        Assert.Contains("60 Hz", warning);
        Assert.Contains("144", warning);
    }

    [Fact]
    public void No_warning_when_panel_matches()
    {
        Assert.Null(OverlayAdvisor.Warn(144, 144, 1920, 1080));
    }

    [Fact]
    public void Parses_host_log_capture_and_request()
    {
        const string log = """
            Info: Display refresh rate [59.79Hz]
            Info: Requested frame rate [144fps]
            """;
        Assert.Equal(60, HostLogStatsParser.TryCaptureHz(log));
        Assert.Equal(144, HostLogStatsParser.TryRequestedFps(log));
    }

    [Fact]
    public void Overlay_line_includes_all_three_clocks()
    {
        var stats = new StreamStats(144, 144, 144, 143, null);
        Assert.Contains("captura 144", stats.OverlayLine);
        Assert.Contains("decode 143", stats.OverlayLine);
        Assert.Contains("panel 144 Hz", stats.OverlayLine);
    }
}
