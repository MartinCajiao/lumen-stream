using Lumen.Core.Quality;
using Xunit;

namespace Lumen.Tests;

public sealed class FpsPresetTests
{
    [Fact]
    public void Native_uses_panel_hz()
    {
        Assert.Equal(144, FpsPreset.Native.Resolve(144));
        Assert.Equal(60, FpsPreset.Native.Resolve(0));
    }

    [Fact]
    public void Fixed_presets_fall_back_to_panel_cap()
    {
        Assert.Equal(144, FpsPreset.Hz144.Resolve(165));
        Assert.Equal(60, FpsPreset.Hz240.Resolve(60));
        Assert.Equal(165, FpsPreset.Hz200.Resolve(165));
        Assert.Equal(200, FpsPreset.Hz200.Resolve(240));
        Assert.Equal(200, FpsPreset.Hz200.Resolve(200));
        Assert.Equal(144, FpsPreset.Hz240.Resolve(144));
        Assert.Equal(240, FpsPreset.Hz240.Resolve(0));
    }

    [Fact]
    public void All_expected_rates_are_first_class()
    {
        var values = FpsPresetExtensions.All.Select(p => p.Resolve(240)).ToArray();
        Assert.Contains(60, values);
        Assert.Contains(90, values);
        Assert.Contains(120, values);
        Assert.Contains(144, values);
        Assert.Contains(165, values);
        Assert.Contains(200, values);
        Assert.Contains(240, values);
    }
}