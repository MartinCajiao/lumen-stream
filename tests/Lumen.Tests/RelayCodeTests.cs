using Lumen.Core.Wan.Relay;
using Xunit;

namespace Lumen.Tests;

public class RelayCodeTests
{
    [Fact]
    public void Parses_host_port_secret()
    {
        var ep = RelayEndpoint.TryParse("relay.example.com:47991:secret123");
        Assert.NotNull(ep);
        Assert.Equal("relay.example.com", ep!.Host);
        Assert.Equal(47991, ep.Port);
        Assert.Equal("secret123", ep.Secret);
    }

    [Fact]
    public void Parses_without_secret()
    {
        var ep = RelayEndpoint.TryParse("1.2.3.4:47991");
        Assert.NotNull(ep);
        Assert.Equal("", ep!.Secret);
    }

    [Fact]
    public void Rejects_bad_input()
    {
        Assert.Null(RelayEndpoint.TryParse(null));
        Assert.Null(RelayEndpoint.TryParse(""));
        Assert.Null(RelayEndpoint.TryParse("no-port"));
        Assert.Null(RelayEndpoint.TryParse(":47991"));
    }

    [Fact]
    public void Parses_relay_share_code()
    {
        var code = RelayCode.TryParse("relay:123456@relay.example.com:47991:secret");
        Assert.NotNull(code);
        Assert.Equal("123456", code!.Code);
        Assert.Equal("relay.example.com", code.Relay.Host);
        Assert.Equal(47991, code.Relay.Port);
        Assert.Equal("secret", code.Relay.Secret);
    }

    [Fact]
    public void Round_trips_to_string()
    {
        var code = RelayCode.TryParse("relay:123456@relay.example.com:47991:secret");
        Assert.Equal("relay:123456@relay.example.com:47991:secret", code!.ToString());
    }

    [Fact]
    public void Detects_relay_code()
    {
        Assert.True(RelayCode.IsRelayCode("relay:123456@host:47991"));
        Assert.False(RelayCode.IsRelayCode("192.168.1.5"));
        Assert.False(RelayCode.IsRelayCode("100.64.1.2"));
    }

    [Fact]
    public void Non_relay_code_returns_null()
    {
        Assert.Null(RelayCode.TryParse("192.168.1.5"));
        Assert.Null(RelayCode.TryParse("100.64.1.2"));
        Assert.Null(RelayCode.TryParse("relay:nohost@:47991"));
    }
}
