namespace Lumen.Core.Wan;

public sealed record WanEndpoint(
    string Address,
    string Method,
    bool RouterOpened,
    string? PublicIpv4,
    string? PublicIpv6,
    bool FirewallOk = true);

public static class WanBootstrap
{
    public static string PickShareAddress(
        string? tailscale,
        string? publicIpv4,
        string? ipv6,
        string? local,
        bool routerOpened,
        NatKind nat)
    {
        // Tailscale is the most reliable cross-network path when available.
        if (!string.IsNullOrWhiteSpace(tailscale))
        {
            return tailscale;
        }

        // IPv6 is end-to-end: no NAT, no UPnP, no relay needed. This is the
        // fully autonomous cross-network path when both ISPs give IPv6.
        if (!string.IsNullOrWhiteSpace(ipv6))
        {
            return ipv6;
        }

        // Public IPv4 with a router that actually opened ports (no CGNAT).
        if (!string.IsNullOrWhiteSpace(publicIpv4)
            && !NetworkAddresses.IsPrivateIpv4(publicIpv4)
            && nat != NatKind.Cgnat
            && nat != NatKind.DoubleNat)
        {
            return publicIpv4;
        }

        // CGNAT / double NAT: a public IPv4 code would be a lie. Hand back the
        // LAN code (works at home) and let the UI explain the rest.
        return local ?? "127.0.0.1";
    }

    public static string Describe(string address, string? tailscale, bool routerOpened, NatKind nat)
    {
        if (!string.IsNullOrWhiteSpace(tailscale) && address == tailscale)
        {
            return "Tailscale";
        }

        if (!string.IsNullOrWhiteSpace(address) && address.Contains(':', StringComparison.Ordinal))
        {
            return "IPv6";
        }

        if (!NetworkAddresses.IsPrivateIpv4(address))
        {
            if (nat is NatKind.Cgnat or NatKind.DoubleNat)
            {
                return "Internet (prueba)";
            }

            return routerOpened ? "Internet" : "Internet (prueba)";
        }

        return "Esta wifi";
    }

    public static string ShareHint(string method) => method switch
    {
        "Tailscale" => "Ese código vale desde otra casa si las dos PCs tienen Tailscale (misma cuenta).",
        "IPv6" => "Ese código es IPv6: va desde otra casa SIN instalar nada, si el otro internet también tiene IPv6.",
        "Internet" => "El router abrió puertos. Si desde otra casa no entra, tu ISP es CGNAT: instala Tailscale en las dos.",
        "Internet (prueba)" => "Tu router no abrió los puertos (UPnP apagado o CGNAT). Pruébalo igual; si no entra, instala Tailscale en las dos PCs y comparte otra vez.",
        _ => "Ese código solo vale en esta wifi. En la casa de tu tía las redes no se ven. Instala Tailscale (gratis) en las dos PCs, comparte otra vez, y usa el código 100.x."
    };

    public static async Task<WanEndpoint> OpenAsync(string? hostExe, int httpPort, CancellationToken token)
    {
        var firewallOk = HostFirewall.TryAllowHost(hostExe);
        var local = NetworkAddresses.LocalIpv4();
        var publicIp = await StunClient.QueryPublicIpv4Async(token).ConfigureAwait(false);
        var ipv6 = NetworkAddresses.GlobalIpv6();
        var tailscale = NetworkAddresses.TailscaleIpv4();
        var mapped = 0;
        if (!string.IsNullOrWhiteSpace(local))
        {
            try
            {
                mapped = await UpnpMapper.MapAsync(local, httpPort, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                mapped = 0;
            }
        }

        var opened = mapped >= 4;
        var nat = await NatProbe.DetectAsync(publicIp, token).ConfigureAwait(false);
        var address = PickShareAddress(tailscale, publicIp, ipv6, local, opened, nat);
        var method = Describe(address, tailscale, opened, nat);
        return new WanEndpoint(address, method, opened, publicIp, ipv6, firewallOk);
    }
}
