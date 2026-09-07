using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace Lumen.Core.Wan.Relay;

/// <summary>
/// Runs on the host (gamer) PC. Connects to a Lumen relay, registers with a
/// shared secret, and gets back a 6-digit code that the user shares. When a
/// client opens a stream through the relay, this connects to the local Apollo
/// port and shuttles bytes. The host needs no inbound port — the relay reaches
/// it through this persistent outbound control connection, so it works behind
/// CGNAT/double NAT without any overlay app.
/// </summary>
public sealed class RelayHostClient : IDisposable
{
    private readonly string _relayHost;
    private readonly int _relayPort;
    private readonly string _sharedSecret;
    private TcpClient? _control;
    private NetworkStream? _stream;
    private readonly ConcurrentDictionary<int, ILocalStream> _streams = new();
    private CancellationTokenSource? _cts;

    public RelayHostClient(string relayHost, int relayPort, string sharedSecret)
    {
        _relayHost = relayHost;
        _relayPort = relayPort;
        _sharedSecret = sharedSecret;
    }

    /// <summary>The 6-digit code returned by the relay, or null until connected.</summary>
    public string? Code { get; private set; }

    public async Task<string> ConnectAsync(CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _control = new TcpClient();
        await _control.ConnectAsync(_relayHost, _relayPort, _cts.Token).ConfigureAwait(false);
        _stream = _control.GetStream();
        await RelayServer.WriteLineAsync(_stream, $"HOST {_sharedSecret}", _cts.Token).ConfigureAwait(false);
        var reply = await RelayServer.ReadLineAsync(_stream, _cts.Token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El relay no respondió.");
        if (!reply.StartsWith("OK ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Relay rechazó: {reply}");
        }

        Code = reply[3..].Trim();
        _ = RunPumpAsync(_cts.Token);
        return Code;
    }

    private async Task RunPumpAsync(CancellationToken token)
    {
        var stream = _stream!;
        while (!token.IsCancellationRequested)
        {
            var frame = await RelayFrame.ReadAsync(stream, token).ConfigureAwait(false);
            if (frame is null)
            {
                return;
            }

            var (type, sessionId, payload) = frame.Value;
            switch (type)
            {
                case RelayFrame.Open:
                    await OpenLocalAsync(sessionId, payload, token).ConfigureAwait(false);
                    break;
                case RelayFrame.Data:
                    if (_streams.TryGetValue(sessionId, out var local))
                    {
                        local.ForwardToLocal(payload);
                    }

                    break;
                case RelayFrame.Close:
                    CloseStream(sessionId);
                    break;
            }
        }
    }

    private async Task OpenLocalAsync(int sessionId, byte[] payload, CancellationToken token)
    {
        // payload: [2-byte port BE][1-byte proto 0=tcp,1=udp]
        if (payload.Length < 3)
        {
            await SendAsync(RelayFrame.Failed, sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
            return;
        }

        var port = (payload[0] << 8) | payload[1];
        var proto = payload[2];
        try
        {
            if (proto == 1)
            {
                var udp = new UdpClient();
                udp.Connect(System.Net.IPAddress.Loopback, port);
                var local = new LocalUdpStream(udp, sessionId, this);
                _streams[sessionId] = local;
                await SendAsync(RelayFrame.Opened, sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
                _ = local.PumpLocalToRelayAsync(token);
            }
            else
            {
                var client = new TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Loopback, port, token).ConfigureAwait(false);
                var local = new LocalStream(client.GetStream(), sessionId, this);
                _streams[sessionId] = local;
                await SendAsync(RelayFrame.Opened, sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
                _ = local.PumpLocalToRelayAsync(token); // long-lived; do not await
            }
        }
        catch (Exception)
        {
            await SendAsync(RelayFrame.Failed, sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
        }
    }

    internal async Task SendAsync(byte type, int sessionId, byte[] payload, CancellationToken token)
    {
        var stream = _stream;
        if (stream is null)
        {
            return;
        }

        try
        {
            var frame = RelayFrame.Encode(type, sessionId, payload);
            await stream.WriteAsync(frame, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // relay gone
        }
    }

    private void CloseStream(int sessionId)
    {
        if (_streams.TryRemove(sessionId, out var local))
        {
            local.Dispose();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        foreach (var s in _streams.Values)
        {
            s.Dispose();
        }

        _streams.Clear();
        _stream?.Dispose();
        _control?.Dispose();
    }

    private interface ILocalStream : IDisposable
    {
        void ForwardToLocal(byte[] data);
        Task PumpLocalToRelayAsync(CancellationToken token);
    }

    private sealed class LocalStream : ILocalStream
    {
        private readonly NetworkStream _local;
        private readonly int _sessionId;
        private readonly RelayHostClient _owner;

        public LocalStream(NetworkStream local, int sessionId, RelayHostClient owner)
        {
            _local = local;
            _sessionId = sessionId;
            _owner = owner;
        }

        public void ForwardToLocal(byte[] data)
        {
            try
            {
                _local.Write(data);
                _local.Flush();
            }
            catch (Exception)
            {
                // local closed
            }
        }

        public async Task PumpLocalToRelayAsync(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var read = await _local.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await _owner.SendAsync(RelayFrame.Data, _sessionId, buffer.AsSpan(0, read).ToArray(), token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // local closed
            }
            finally
            {
                await _owner.SendAsync(RelayFrame.Close, _sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            try { _local.Dispose(); }
            catch { }
        }
    }

    private sealed class LocalUdpStream : ILocalStream
    {
        private readonly UdpClient _udp;
        private readonly int _sessionId;
        private readonly RelayHostClient _owner;

        public LocalUdpStream(UdpClient udp, int sessionId, RelayHostClient owner)
        {
            _udp = udp;
            _sessionId = sessionId;
            _owner = owner;
        }

        public void ForwardToLocal(byte[] data)
        {
            try
            {
                _udp.Send(data, data.Length);
            }
            catch (Exception)
            {
                // local closed
            }
        }

        public async Task PumpLocalToRelayAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var received = await _udp.ReceiveAsync(token).ConfigureAwait(false);
                    await _owner.SendAsync(RelayFrame.Data, _sessionId, received.Buffer, token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // local closed
            }
            finally
            {
                await _owner.SendAsync(RelayFrame.Close, _sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            try { _udp.Dispose(); }
            catch { }
        }
    }
}
