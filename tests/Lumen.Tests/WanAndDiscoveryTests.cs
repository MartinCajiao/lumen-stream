using Lumen.Core.Discovery;
using Lumen.Core.Runtime;
using Lumen.Core.Wan;
using Xunit;

namespace Lumen.Tests;

public sealed class WanAndDiscoveryTests
{
    [Fact]
    public void Invite_link_encodes_host_and_port()
    {
        var wan = WanSettings.ForInternet();
        Assert.Equal("lumen://connect?host=10.0.0.4&port=47989", wan.InviteLink("10.0.0.4"));
        Assert.Contains("STUN", string.Join(' ', wan.RelayNotes()));
        Assert.Contains("Tailscale", string.Join(' ', wan.RelayNotes()));
    }

    [Fact]
    public void Stun_parses_xor_mapped_ipv4()
    {
        // Binding success + XOR-MAPPED-ADDRESS for 1.2.3.4
        var msg = new byte[32];
        msg[0] = 0x01;
        msg[1] = 0x01;
        msg[2] = 0x00;
        msg[3] = 0x0C;
        msg[4] = 0x21;
        msg[5] = 0x12;
        msg[6] = 0xA4;
        msg[7] = 0x42;
        msg[20] = 0x00;
        msg[21] = 0x20;
        msg[22] = 0x00;
        msg[23] = 0x08;
        msg[25] = 0x01;
        msg[26] = 0x21;
        msg[27] = 0x12;
        msg[28] = 0x21 ^ 1;
        msg[29] = 0x12 ^ 2;
        msg[30] = 0xA4 ^ 3;
        msg[31] = 0x42 ^ 4;
        Assert.True(StunClient.TryParseMappedIpv4(msg, out var ip));
        Assert.Equal("1.2.3.4", ip);
    }

    [Fact]
    public void Private_lan_ips_are_detected()
    {
        Assert.True(NetworkAddresses.IsPrivateIpv4("192.168.1.10"));
        Assert.True(NetworkAddresses.IsPrivateIpv4("10.0.0.8"));
        Assert.False(NetworkAddresses.IsPrivateIpv4("8.8.8.8"));
    }

    [Fact]
    public void GameStream_ports_match_sunshine_offsets()
    {
        var ports = GameStreamPorts.ForBase(47989);
        Assert.Contains(ports, p => p.Number == 47989 && p.Protocol == "TCP");
        Assert.Contains(ports, p => p.Number == 47984 && p.Protocol == "TCP");
        Assert.Contains(ports, p => p.Number == 47998 && p.Protocol == "UDP");
        Assert.Contains(ports, p => p.Number == 48010 && p.Protocol == "TCP");
    }

    [Fact]
    public void Share_address_prefers_public_ip_over_lan()
    {
        var picked = WanBootstrap.PickShareAddress(null, "203.0.113.8", null, "192.168.1.4", routerOpened: true);
        Assert.Equal("203.0.113.8", picked);
        Assert.Equal("100.64.1.2", WanBootstrap.PickShareAddress("100.64.1.2", "203.0.113.8", null, "192.168.1.4", true));
    }

    [Fact]
    public void Without_upnp_share_code_keeps_public_ip_with_honest_warning()
    {
        var picked = WanBootstrap.PickShareAddress(null, "203.0.113.8", null, "192.168.1.4", routerOpened: false);
        Assert.Equal("203.0.113.8", picked);
        Assert.Equal("Internet (prueba)", WanBootstrap.Describe(picked, null, false));
        Assert.Contains("Tailscale", WanBootstrap.ShareHint("Internet (prueba)"));
        Assert.Contains("UPnP", WanBootstrap.ShareHint("Internet (prueba)"));

        var lan = WanBootstrap.PickShareAddress(null, null, null, "192.168.1.4", routerOpened: false);
        Assert.Equal("192.168.1.4", lan);
        Assert.Equal("Esta wifi", WanBootstrap.Describe(lan, null, false));
    }

    [Fact]
    public void Unreachable_lan_ip_explains_other_house()
    {
        var msg = HostProbe.ExplainUnreachable("192.168.1.20");
        Assert.Contains("Tailscale", msg);
        Assert.Contains("wifi", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100.x", HostProbe.ExplainUnreachable("100.64.1.2"));
    }

    [Fact]
    public void Upnp_ssdp_and_soap_are_well_formed()
    {
        var location = UpnpMapper.TryParseSsdpLocation("HTTP/1.1 200 OK\r\nLOCATION: http://192.168.1.1:5000/root.xml\r\n");
        Assert.Equal("http://192.168.1.1:5000/root.xml", location);
        Assert.Equal("http://192.168.1.1:5000/ctl", UpnpMapper.CombineUrl("http://192.168.1.1:5000/root.xml", "/ctl"));
        var soap = UpnpMapper.AddPortMappingEnvelope("urn:schemas-upnp-org:service:WANIPConnection:1", 47989, "TCP", "192.168.1.10");
        Assert.Contains("<NewExternalPort>47989</NewExternalPort>", soap);
        Assert.Contains("<NewInternalClient>192.168.1.10</NewInternalClient>", soap);
    }

    [Fact]
    public void Beacon_roundtrip()
    {
        var host = new DiscoveredHost("Sala", "192.168.1.20", 47989, 144);
        var bytes = LanBeacon.Encode(host);
        var decoded = LanBeacon.TryDecode(bytes, bytes.Length);
        Assert.Equal(host, decoded);
        Assert.Contains("144", host.Label);
    }

    [Fact]
    public void Garbage_is_ignored()
    {
        var junk = "nope"u8.ToArray();
        Assert.Null(LanBeacon.TryDecode(junk, junk.Length));
    }
}
