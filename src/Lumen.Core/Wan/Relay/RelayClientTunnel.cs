using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Lumen.Core.Wan.Relay;

/// <summary>
/// Runs on the client PC. Connects to a Lumen relay with the host's code, then
/// opens local TCP listeners (127.0.0.1) for each GameStream port Moonlight needs.
/// When Moonlight connects to a local listener, this opens a multiplexed stream
/// through the relay to the host's Apollo port and shuttles bytes. Moonlight points
/// at 127.0.0.1, so it needs no overlay app and no inbound port on either side.
/// </summary>
public sealed class RelayClientTunnel : IDisposable
{
    private readonly string _relayHost;
    private readonly int _relayPort;
    private readonly string _code;
    private TcpClient? _control;
    private NetworkStream? _stream;
    private readonly ConcurrentDictionary<int, IDisposable> _listeners = new();
    private readonly ConcurrentDictionary<int, IClientStream> _streams = new();
    private int _nextSessionId = 1;
    private CancellationTokenSource? _cts;

    public RelayClientTunnel(string relayHost, int relayPort, string code)
    {
        _relayHost = relayHost;
        _relayPort = relayPort;
        _code = code;
    }

    /// <summary>Local address Moonlight should connect to once started.</summary>
    public string LocalTarget { get; private set; } = "127.0.0.1";

    public async Task StartAsync(CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _control = new TcpClient();
        await _control.ConnectAsync(_relayHost, _relayPort, _cts.Token).ConfigureAwait(false);
        _stream = _control.GetStream();
        await RelayServer.WriteLineAsync(_stream, $"CONNECT {_code}", _cts.Token).ConfigureAwait(false);
        var reply = await RelayServer.ReadLineAsync(_stream, _cts.Token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El relay no respondió.");
        if (!reply.Equals("OK", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Relay rechazó el código: {reply}. Vuelve a compartir en el PC gamer.");
        }

        _ = RunPumpAsync(_cts.Token);
    }

    /// <summary>Opens a local TCP listener that tunnels to the host's <paramref name="hostPort"/>.</summary>
    public int ListenFor(int hostPort)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var entry = new LocalListener(listener, hostPort, this);
        _listeners[hostPort] = entry;
        _ = entry.AcceptAsync(_cts?.Token ?? CancellationToken.None);
        return localPort;
    }

    /// <summary>Opens a local UDP listener that tunnels to the host's <paramref name="hostPort"/> (GameStream video/audio).</summary>
    public int ListenForUdp(int hostPort)
    {
        var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var localPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var entry = new LocalUdpListener(udp, hostPort, this);
        _listeners[hostPort] = entry;
        _ = entry.PumpAsync(_cts?.Token ?? CancellationToken.None);
        return localPort;
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
                case RelayFrame.Opened:
                    if (_streams.TryGetValue(sessionId, out var opened))
                    {
                        opened.OnOpened();
                    }

                    break;
                case RelayFrame.Failed:
                    if (_streams.TryGetValue(sessionId, out var failed))
                    {
                        failed.OnFailed();
                    }

                    break;
                case RelayFrame.Data:
                    if (_streams.TryGetValue(sessionId, out var target))
                    {
                        target.ForwardToMoonlight(payload);
                    }

                    break;
                case RelayFrame.Close:
                    if (_streams.TryRemove(sessionId, out var closed))
                    {
                        closed.Dispose();
                    }

                    break;
            }
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

    internal int AllocateSessionId() => Interlocked.Increment(ref _nextSessionId);

    internal void RegisterStream(int sessionId, IClientStream stream) => _streams[sessionId] = stream;

    internal void RemoveStream(int sessionId)
    {
        _streams.TryRemove(sessionId, out _);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        foreach (var l in _listeners.Values)
        {
            l.Dispose();
        }

        foreach (var s in _streams.Values)
        {
            s.Dispose();
        }

        _listeners.Clear();
        _streams.Clear();
        _stream?.Dispose();
        _control?.Dispose();
    }

    internal sealed class LocalListener : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _hostPort;
        private readonly RelayClientTunnel _owner;

        public LocalListener(TcpListener listener, int hostPort, RelayClientTunnel owner)
        {
            _listener = listener;
            _hostPort = hostPort;
            _owner = owner;
        }

        public async Task AcceptAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                _ = ServeAsync(client, token);
            }
        }

        private async Task ServeAsync(TcpClient moonlight, CancellationToken token)
        {
            var sessionId = _owner.AllocateSessionId();
            var stream = new ClientStream(moonlight.GetStream(), sessionId, _hostPort, _owner);
            _owner.RegisterStream(sessionId, stream);
            var portBytes = new byte[] { (byte)(_hostPort >> 8), (byte)_hostPort, 0 }; // proto 0 = TCP
            await _owner.SendAsync(RelayFrame.Open, sessionId, portBytes, token).ConfigureAwait(false);
            await stream.PumpMoonlightToRelayAsync(token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            try { _listener.Stop(); }
            catch { }
        }
    }

    internal sealed class LocalUdpListener : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly int _hostPort;
        private readonly RelayClientTunnel _owner;
        private IPEndPoint? _moonlightEndpoint;

        public LocalUdpListener(UdpClient udp, int hostPort, RelayClientTunnel owner)
        {
            _udp = udp;
            _hostPort = hostPort;
            _owner = owner;
        }

        public async Task PumpAsync(CancellationToken token)
        {
            // Moonlight sends UDP to this local port. We capture its source endpoint,
            // open a relay stream to the host's UDP port, and forward datagrams both ways.
            var sessionId = _owner.AllocateSessionId();
            var stream = new ClientUdpStream(sessionId, _hostPort, _owner, this);
            _owner.RegisterStream(sessionId, stream);
            var portBytes = new byte[] { (byte)(_hostPort >> 8), (byte)_hostPort, 1 }; // proto 1 = UDP
            await _owner.SendAsync(RelayFrame.Open, sessionId, portBytes, token).ConfigureAwait(false);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var received = await _udp.ReceiveAsync(token).ConfigureAwait(false);
                    _moonlightEndpoint = received.RemoteEndPoint;
                    await _owner.SendAsync(RelayFrame.Data, sessionId, received.Buffer, token)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // closed
            }
        }

        public void ForwardToMoonlight(byte[] data)
        {
            if (_moonlightEndpoint is null)
            {
                return;
            }

            try
            {
                _udp.Send(data, data.Length, _moonlightEndpoint);
            }
            catch (Exception)
            {
                // moonlight gone
            }
        }

        public void Dispose()
        {
            try { _udp.Dispose(); }
            catch { }
        }
    }

    internal sealed class ClientUdpStream : IClientStream
    {
        private readonly int _sessionId;
        private readonly RelayClientTunnel _owner;
        private readonly LocalUdpListener _listener;

        public ClientUdpStream(int sessionId, int hostPort, RelayClientTunnel owner, LocalUdpListener listener)
        {
            _sessionId = sessionId;
            _owner = owner;
            _listener = listener;
        }

        public void ForwardToMoonlight(byte[] data) => _listener.ForwardToMoonlight(data);

        public void Dispose()
        {
            _owner.RemoveStream(_sessionId);
        }
    }

    internal interface IClientStream : IDisposable
    {
        void ForwardToMoonlight(byte[] data);
        void OnOpened() { }
        void OnFailed() { }
    }

    internal sealed class ClientStream : IClientStream
    {
        private readonly NetworkStream _moonlight;
        private readonly int _sessionId;
        private readonly RelayClientTunnel _owner;
        private readonly TaskCompletionSource _opened = new();

        public ClientStream(NetworkStream moonlight, int sessionId, int hostPort, RelayClientTunnel owner)
        {
            _moonlight = moonlight;
            _sessionId = sessionId;
            _owner = owner;
        }

        public void OnOpened() => _opened.TrySetResult();
        public void OnFailed() => _opened.TrySetException(new InvalidOperationException("El PC gamer rechazó el puerto."));

        public void ForwardToMoonlight(byte[] data)
        {
            try
            {
                _moonlight.Write(data);
                _moonlight.Flush();
            }
            catch (Exception)
            {
                // moonlight closed
            }
        }

        public async Task PumpMoonlightToRelayAsync(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var read = await _moonlight.ReadAsync(buffer, token).ConfigureAwait(false);
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
                // moonlight closed
            }
            finally
            {
                await _owner.SendAsync(RelayFrame.Close, _sessionId, Array.Empty<byte>(), token).ConfigureAwait(false);
                _owner.RemoveStream(_sessionId);
            }
        }

        public void Dispose()
        {
            try { _moonlight.Dispose(); }
            catch { }
        }
    }
}
