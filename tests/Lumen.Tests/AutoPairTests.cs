using Lumen.Core.Host;
using Lumen.Core.Runtime;
using Xunit;

namespace Lumen.Tests;

public sealed class AutoPairTests
{
    [Fact]
    public void Lumen_auto_pin_is_four_digits()
    {
        Assert.Equal(4, PairingClient.LumenAutoPin.Length);
        Assert.All(PairingClient.LumenAutoPin, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void Pair_args_carry_the_fixed_lumen_pin()
    {
        var args = SessionLauncher.ClientArgs("192.168.10.173", pairOnly: true);
        Assert.Contains("pair 192.168.10.173", args);
        Assert.Contains($"--pin {PairingClient.LumenAutoPin}", args);
    }

    [Fact]
    public void Stream_args_do_not_carry_a_pin()
    {
        var args = SessionLauncher.ClientArgs("192.168.10.173", pairOnly: false);
        Assert.Contains("stream 192.168.10.173 Desktop", args);
        Assert.DoesNotContain("--pin", args);
    }

    [Fact]
    public void Pair_args_bracket_ipv6_hosts()
    {
        var args = SessionLauncher.ClientArgs("fe80::1", pairOnly: true);
        Assert.Contains("pair [fe80::1]", args);
    }

    [Fact]
    public void Empty_host_opens_moonlight_with_no_args()
    {
        Assert.Equal("", SessionLauncher.ClientArgs(null, pairOnly: false));
        Assert.Equal("", SessionLauncher.ClientArgs("   ", pairOnly: true));
    }
}
