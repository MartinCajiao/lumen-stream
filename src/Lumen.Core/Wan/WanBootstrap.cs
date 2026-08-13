namespace Lumen.Core.Wan;

public sealed record WanEndpoint(string Address, string Method, bool RouterOpened, string? PublicIpv4);

public static class WanBootstrap
{
    public static string PickShareAddress(
        string? tailscale,
        string? publicIpv4,
        string? ipv6,
        string? local,
        bool routerOpened)
    {
        if (!string.IsNullOrWhiteSpace(tailscale))
        {
            return tailscale;
        }

        if (routerOpened && !string.IsNullOrWhiteSpace(publicIpv4) && !NetworkAddresses.IsPrivateIpv4(publicIpv4))
        {
            return publicIpv4;
        }

        if (routerOpened && !string.IsNullOrWhiteSpace(ipv6))
        {
            return ipv6;
        }

        return local ?? "127.0.0.1";
    }

    public static string Describe(string address, string? tailscale, bool routerOpened)
    {
        if (!string.IsNullOrWhiteSpace(tailscale) && address == tailscale)
        {
            return "Tailscale";
        }

        if (routerOpened && !NetworkAddresses.IsPrivateIpv4(address))
        {
            return "Internet";
        }

        return "Esta wifi";
    }

    public static string ShareHint(string method) => method switch
    {
        "Tailscale" => "Ese código vale desde otra casa si las dos PCs tienen Tailscale (misma cuenta).",
        "Internet" => "El router abrió puertos. Si desde otra casa no entra, tu ISP es CGNAT: instala Tailscale en las dos.",
        _ => "Ese código solo vale en esta wifi. En la casa de tu tía las redes no se ven. Instala Tailscale (gratis) en las dos PCs, comparte otra vez, y usa el código 100.x."
    };

    public static async Task<WanEndpoint> OpenAsync(string? hostExe, int httpPort, CancellationToken token)
    {
        HostFirewall.TryAllowHost(hostExe);
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
        var address = PickShareAddress(tailscale, publicIp, ipv6, local, opened);
        var method = Describe(address, tailscale, opened);
        return new WanEndpoint(address, method, opened, publicIp);
    }
}
