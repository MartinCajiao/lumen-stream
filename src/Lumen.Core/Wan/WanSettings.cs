namespace Lumen.Core.Wan;

public sealed record WanSettings
{
    public bool EnableUpnp { get; init; }
    public bool EnableStun { get; init; }
    public string StunServer { get; init; } = "stun:stun.l.google.com:19302";
    public string TurnServer { get; init; } = "";
    public string TurnUsername { get; init; } = "";
    public string TurnPassword { get; init; } = "";
    public string ExternalIp { get; init; } = "";
    public bool PreferTailscale { get; init; } = true;
    public int HostPort { get; init; } = 47989;
    /// <summary>Lumen self-hosted relay, format "host:port:secret". Empty = no relay.</summary>
    public string RelayServer { get; init; } = "";

    public static WanSettings Disabled { get; } = new();

    public static WanSettings ForInternet(string? externalIp = null) => new()
    {
        EnableUpnp = true,
        EnableStun = true,
        ExternalIp = externalIp ?? "",
        PreferTailscale = true,
        HostPort = 47989
    };

    public string InviteLink(string host) =>
        $"lumen://connect?host={Uri.EscapeDataString(host)}&port={HostPort}";

    public IReadOnlyList<string> RelayNotes()
    {
        var notes = new List<string>
        {
            "Parsec cobra un relay global. Lumen no opera relays de pago: STUN es gratis; TURN y Tailscale los alojas tú."
        };
        if (PreferTailscale)
        {
            notes.Add("Recomendado: Tailscale o WireGuard entre host y cliente. El stream va P2P por esa red.");
        }

        if (EnableUpnp)
        {
            notes.Add("UPnP intentará abrir los puertos GameStream (47984–48010) en el router.");
        }

        if (!string.IsNullOrWhiteSpace(TurnServer))
        {
            notes.Add($"TURN self-hosted: {TurnServer}. Úsalo solo si el NAT es simétrico y no hay overlay VPN.");
        }

        return notes;
    }
}
