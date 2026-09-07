namespace Lumen.Core.Wan.Relay;

/// <summary>Parsed relay server endpoint: host, port, shared secret.</summary>
public sealed record RelayEndpoint(string Host, int Port, string Secret)
{
    public override string ToString() => string.IsNullOrEmpty(Secret)
        ? $"{Host}:{Port}"
        : $"{Host}:{Port}:{Secret}";

    /// <summary>Parses "host:port" or "host:port:secret". Returns null if invalid.</summary>
    public static RelayEndpoint? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parts = raw.Trim().Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var port))
        {
            return null;
        }

        var host = parts[0].Trim();
        var secret = parts.Length > 2 ? parts[2].Trim() : "";
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return new RelayEndpoint(host, port, secret);
    }
}

/// <summary>A share code that points through a relay: "relay:123456@host:47991".</summary>
public sealed record RelayCode(string Code, RelayEndpoint Relay)
{
    public override string ToString() => $"relay:{Code}@{Relay}";

    /// <summary>Parses "relay:123456@host:47991" or "relay:123456@host:47991:secret". Returns null if not a relay code.</summary>
    public static RelayCode? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (!text.StartsWith("relay:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = text[6..];
        var at = rest.IndexOf('@');
        if (at <= 0)
        {
            return null;
        }

        var code = rest[..at].Trim();
        var endpoint = RelayEndpoint.TryParse(rest[(at + 1)..]);
        if (endpoint is null || code.Length == 0)
        {
            return null;
        }

        return new RelayCode(code, endpoint);
    }

    public static bool IsRelayCode(string? raw)
        => !string.IsNullOrWhiteSpace(raw) && raw.Trim().StartsWith("relay:", StringComparison.OrdinalIgnoreCase);
}
