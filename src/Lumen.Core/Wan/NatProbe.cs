namespace Lumen.Core.Wan;

public enum NatKind
{
    /// <summary>No UPnP gateway answered; we cannot tell.</summary>
    Unknown,

    /// <summary>Router WAN IP == public IP: inbound port mapping can work.</summary>
    Open,

    /// <summary>Router WAN IP is private: there is another NAT in front (ISP CGNAT or a second router).</summary>
    DoubleNat,

    /// <summary>Router WAN IP is public but different from the public IP: ISP-level NAT (CGNAT).</summary>
    Cgnat
}

public static class NatProbe
{
    public static NatKind Classify(string? publicIp, string? routerWanIp)
    {
        if (string.IsNullOrWhiteSpace(routerWanIp))
        {
            return NatKind.Unknown;
        }

        if (NetworkAddresses.IsPrivateIpv4(routerWanIp))
        {
            return NatKind.DoubleNat;
        }

        if (!string.IsNullOrWhiteSpace(publicIp)
            && string.Equals(publicIp.Trim(), routerWanIp.Trim(), StringComparison.Ordinal))
        {
            return NatKind.Open;
        }

        return NatKind.Cgnat;
    }

    public static bool BlocksInternet(NatKind kind) =>
        kind is NatKind.DoubleNat or NatKind.Cgnat;

    public static string Explain(NatKind kind) => kind switch
    {
        NatKind.DoubleNat => "Tu router está detrás de otro NAT (doble NAT o CGNAT del ISP). Los puertos que abre UPnP no llegan a internet: el código público no funciona desde otra casa.",
        NatKind.Cgnat => "Tu ISP te mete en CGNAT: la IP pública no es tuya. Nada que abras en el router llega a internet.",
        NatKind.Open => "El router da directo a internet. Los puertos mapeados deberían funcionar.",
        _ => "No pude preguntarle al router. Si desde otra casa no entra, casi seguro es CGNAT."
    };

    public static async Task<NatKind> DetectAsync(string? publicIp, CancellationToken token)
    {
        var wan = await UpnpMapper.QueryRouterWanIpAsync(token).ConfigureAwait(false);
        return Classify(publicIp, wan);
    }
}
