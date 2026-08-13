using Lumen.Core.Display;
using Lumen.Core.Quality;
using Lumen.Core.Runtime;
using Lumen.Core.Settings;
using Xunit;

namespace Lumen.Tests;

public sealed class EasyUseTests
{
    [Theory]
    [InlineData("martin")]
    [InlineData("Sala")]
    public void Username_without_email_is_ok(string name) =>
        Assert.Null(LocalAccount.ValidateUsername(name));

    [Theory]
    [InlineData("a")]
    [InlineData("yo@correo.com")]
    [InlineData("")]
    public void Email_or_tiny_name_is_rejected(string name) =>
        Assert.NotNull(LocalAccount.ValidateUsername(name));

    [Fact]
    public void Features_are_wired_even_before_binaries()
    {
        var profile = StreamProfile.From(new LumenSettings
        {
            Fps = FpsPreset.Native,
            PrivacyMode = true,
            WacomPressureTilt = true,
            MonitorCount = 2
        }, new DisplayInfo(1920, 1080, 144, "TEST"));

        var checks = FeatureChecker.Evaluate(profile, hostInstalled: false, clientInstalled: false);
        Assert.Contains(checks, c => c.Id == "hz" && c.Ready);
        Assert.Contains(checks, c => c.Id == "privacy" && c.Ready);
        Assert.Contains(checks, c => c.Id == "wacom" && c.Ready);
        Assert.Contains(checks, c => c.Id == "host" && !c.Ready);
        Assert.Contains("Pulsa Compartir", FeatureChecker.Headline(checks));
    }

    [Fact]
    public void Ready_headline_when_both_binaries_exist()
    {
        var profile = StreamProfile.From(new LumenSettings(), new DisplayInfo(1920, 1080, 60, "TEST"));
        var checks = FeatureChecker.Evaluate(profile, true, true);
        Assert.Contains("Listo", FeatureChecker.Headline(checks));
    }
}
