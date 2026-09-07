using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace Lumen.Core.Wan.Relay;

/// <summary>
/// A self-hostable relay for Lumen. One public TCP port carries every GameStream
/// port (multiplexed), so it deploys on any cheap/free cloud VM with a single
/// inbound port. The server is a pure multiplexer: it pairs a host's control
/// connection with a client's and shuttles frames between them. The actual local
/// connections to Apollo (on the host) and Moonlight (on the client) happen on
/// the user's PCs — see <see cref="RelayHostClient"/> and <see cref="RelayClientTunnel"/>.
/// No third-party account, no overlay app on the user's PCs.
/// </summary>
public sealed class RelayServer : IDisposable
{
    private readonly int _controlPort;
    private readonly string _sharedSecret;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly Random _rng = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public RelayServer(int controlPort, string sharedSecret)
    {
        _controlPort = controlPort;
        _sharedSecret = sharedSecret;
    }

    public int ControlPort => _controlPort;
    public int SessionCount => _sessions.Count;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(System.Net.IPAddress.Any, _controlPort);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        foreach (var s in _sessions.Values)
        {
            s.Dispose();
        }

        _sessions.Clear();
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = HandlePeerAsync(client, token);
        }
    }

    private async Task HandlePeerAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var line = await ReadLineAsync(stream, token).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                var parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    return;
                }

                if (parts[0] == "HOST")
                {
                    await HandleHostAsync(client, stream, parts[1], token).ConfigureAwait(false);
                }
                else if (parts[0] == "CONNECT")
                {
                    await HandleClientAsync(client, stream, parts[1], token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // peer misbehaved or disconnected; drop it
            }
        }
    }

    private async Task HandleHostAsync(TcpClient client, NetworkStream stream, string secret, CancellationToken token)
    {
        if (!SecretMatches(secret))
        {
            await WriteLineAsync(stream, "ERR secret", token).ConfigureAwait(false);
            return;
        }

        var code = GenerateCode();
        var session = new Session(code, client, stream);
        while (!_sessions.TryAdd(code, session))
        {
            code = GenerateCode();
            session = new Session(code, client, stream);
        }

        try
        {
            await WriteLineAsync(stream, $"OK {code}", token).ConfigureAwait(false);
            await session.RunHostAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(code, out _);
            session.Dispose();
        }
    }

    private async Task HandleClientAsync(TcpClient client, NetworkStream stream, string code, CancellationToken token)
    {
        if (!_sessions.TryGetValue(code, out var session) || session.HasClient)
        {
            await WriteLineAsync(stream, "ERR code", token).ConfigureAwait(false);
            return;
        }

        if (!session.AttachClient(client, stream))
        {
            await WriteLineAsync(stream, "ERR busy", token).ConfigureAwait(false);
            return;
        }

        try
        {
            await WriteLineAsync(stream, "OK", token).ConfigureAwait(false);
            await session.RunClientAsync(token).ConfigureAwait(false);
        }
        finally
        {
            session.DetachClient();
        }
    }

    private string GenerateCode()
    {
        lock (_rng)
        {
            return _rng.Next(100000, 1000000).ToString();
        }
    }

    private bool SecretMatches(string presented)
        => string.IsNullOrEmpty(_sharedSecret) || ConstantTimeEquals(presented, _sharedSecret);

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    internal static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken token)
    {
        var sb = new StringBuilder();
        var buf = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buf, token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            if (buf[0] == '\n')
            {
                return sb.ToString().TrimEnd('\r');
            }

            sb.Append((char)buf[0]);
            if (sb.Length > 256)
            {
                return null;
            }
        }
    }

    internal static async Task WriteLineAsync(NetworkStream stream, string line, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>One registered host + (optionally) its paired client.</summary>
    private sealed class Session : IDisposable
    {
        private readonly TcpClient _hostClient;
        private readonly NetworkStream _hostStream;
        private TcpClient? _clientClient;
        private NetworkStream? _clientStream;
        private readonly object _lock = new();
        private bool _hasClient;

        public string Code { get; }
        public bool HasClient => _hasClient;

        public Session(string code, TcpClient hostClient, NetworkStream hostStream)
        {
            Code = code;
            _hostClient = hostClient;
            _hostStream = hostStream;
        }

        public bool AttachClient(TcpClient client, NetworkStream stream)
        {
            lock (_lock)
            {
                if (_hasClient)
                {
                    return false;
                }

                _clientClient = client;
                _clientStream = stream;
                _hasClient = true;
                return true;
            }
        }

        public void DetachClient()
        {
            lock (_lock)
            {
                _hasClient = false;
                _clientStream = null;
                _clientClient = null;
            }
        }

        public async Task RunHostAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var frame = await RelayFrame.ReadAsync(_hostStream, token).ConfigureAwait(false);
                if (frame is null)
                {
                    return;
                }

                await ForwardToClientAsync(frame.Value, token).ConfigureAwait(false);
            }
        }

        public async Task RunClientAsync(CancellationToken token)
        {
            var stream = _clientStream!;
            while (!token.IsCancellationRequested)
            {
                var frame = await RelayFrame.ReadAsync(stream, token).ConfigureAwait(false);
                if (frame is null)
                {
                    return;
                }

                await ForwardToHostAsync(frame.Value, token).ConfigureAwait(false);
            }
        }

        private async Task ForwardToClientAsync((byte Type, int SessionId, byte[] Payload) frame, CancellationToken token)
        {
            var stream = _clientStream;
            if (stream is null)
            {
                return;
            }

            try
            {
                var bytes = RelayFrame.Encode(frame.Type, frame.SessionId, frame.Payload);
                await stream.WriteAsync(bytes, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // client gone
            }
        }

        private async Task ForwardToHostAsync((byte Type, int SessionId, byte[] Payload) frame, CancellationToken token)
        {
            try
            {
                var bytes = RelayFrame.Encode(frame.Type, frame.SessionId, frame.Payload);
                await _hostStream.WriteAsync(bytes, token).ConfigureAwait(false);
                await _hostStream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // host gone
            }
        }

        public void Dispose()
        {
            _hostClient.Dispose();
            DetachClient();
        }
    }
}
