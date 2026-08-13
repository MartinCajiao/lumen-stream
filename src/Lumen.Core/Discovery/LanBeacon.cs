using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Lumen.Core.Discovery;

public sealed record DiscoveredHost(string Name, string Address, int Port, int PanelHz, string Owner = "")
{
    public string Label => string.IsNullOrWhiteSpace(Owner)
        ? $"{Name} — {Address}:{Port} · panel {PanelHz} Hz"
        : $"{Owner} · {Name} — {Address}";
}

/// <summary>UDP beacon so Lumen clients find Lumen hosts on LAN without mDNS.</summary>
public static class LanBeacon
{
    public const int Port = 47991;
    public const string Magic = "LUMEN";

    public static byte[] Encode(DiscoveredHost host)
    {
        var json = JsonSerializer.Serialize(host);
        return Encoding.UTF8.GetBytes(Magic + json);
    }

    public static DiscoveredHost? TryDecode(byte[] buffer, int length)
    {
        if (length < Magic.Length)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, length);
        if (!text.StartsWith(Magic, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DiscoveredHost>(text[Magic.Length..]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static UdpClient StartAnnouncing(DiscoveredHost host, CancellationToken token)
    {
        var udp = new UdpClient { EnableBroadcast = true };
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _ = Task.Run(async () =>
        {
            var payload = Encode(host);
            var endpoint = new IPEndPoint(IPAddress.Broadcast, Port);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await udp.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // LAN may be down; keep trying.
                }

                await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            }
        }, token);
        return udp;
    }

    public static async Task<IReadOnlyList<DiscoveredHost>> ScanAsync(TimeSpan duration, CancellationToken token)
    {
        using var udp = new UdpClient(Port);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.EnableBroadcast = true;
        var found = new Dictionary<string, DiscoveredHost>(StringComparer.OrdinalIgnoreCase);
        var until = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < until && !token.IsCancellationRequested)
        {
            var remaining = until - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var receiveTask = udp.ReceiveAsync(token).AsTask();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining, token)).ConfigureAwait(false);
            if (completed != receiveTask)
            {
                break;
            }

            var result = await receiveTask.ConfigureAwait(false);
            var host = TryDecode(result.Buffer, result.Buffer.Length);
            if (host is not null)
            {
                found[$"{host.Address}:{host.Port}"] = host with { Address = result.RemoteEndPoint.Address.ToString() };
            }
        }

        return found.Values.ToList();
    }
}
