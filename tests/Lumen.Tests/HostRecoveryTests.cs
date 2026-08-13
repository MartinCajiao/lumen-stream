using Lumen.Core.Display;
using Lumen.Core.Host;
using Lumen.Core.Paths;
using Lumen.Core.Quality;
using Lumen.Core.Runtime;
using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class HostRecoveryTests
{
    [Fact]
    public void Log_hint_ignores_state_file_info_and_encoder_noise()
    {
        var log = """
            [2026-08-13 11:57:41.324]: Error: NvEnc: gpu doesn't support YUV444 encode
            [2026-08-13 11:57:41.430]: Info: Found HEVC encoder: hevc_nvenc [nvenc]
            [2026-08-13 11:57:41.431]: Info: File C:/Users/marti/AppData/Roaming/LumenStream/host/sunshine_state.json doesn't exist
            """;
        Assert.Null(HostLogHint.FromText(log));
    }

    [Fact]
    public void Log_hint_prefers_fatal_over_noise()
    {
        var log = """
            [2026-08-13 11:57:41.431]: Info: File sunshine_state.json doesn't exist
            [2026-08-13 11:57:41.500]: Fatal: HTTP interface failed to initialize
            """;
        var hint = HostLogHint.FromText(log);
        Assert.NotNull(hint);
        Assert.Contains("HTTP interface failed", hint);
        Assert.DoesNotContain("doesn't exist", hint);
    }

    [Fact]
    public void Fallbacks_add_ipv4_and_no_privacy()
    {
        var wanted = StreamProfile.From(new LumenSettings
        {
            PrivacyMode = true,
            Wan = Lumen.Core.Wan.WanSettings.ForInternet("203.0.113.8")
        }, new DisplayInfo(1920, 1080, 60, "TEST"));

        var plans = HostLaunchPlan.Fallbacks(wanted);
        Assert.True(plans.Count >= 2);
        Assert.Contains(plans, p => p.AddressFamily == "ipv4");
        Assert.Contains(plans, p => !p.PrivacyMode && !p.Wan.EnableUpnp);
        Assert.Equal("both", plans[0].AddressFamily);
        Assert.True(plans[0].PrivacyMode);
    }

    [Fact]
    public void State_file_is_seeded_with_unique_id()
    {
        if (File.Exists(LumenPaths.HostStateFile))
        {
            File.Delete(LumenPaths.HostStateFile);
        }

        SunshineConfigWriter.EnsureStateFile();
        Assert.True(File.Exists(LumenPaths.HostStateFile));
        var json = File.ReadAllText(LumenPaths.HostStateFile);
        Assert.Contains("uniqueid", json, StringComparison.OrdinalIgnoreCase);
        SunshineConfigWriter.EnsureStateFile();
        Assert.Equal(json, File.ReadAllText(LumenPaths.HostStateFile));
    }

    [Fact]
    public void Sweep_zombies_does_not_throw() => HostProcess.SweepZombies();
}
