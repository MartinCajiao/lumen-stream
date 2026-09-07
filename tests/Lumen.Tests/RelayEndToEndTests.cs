using System.Net;
using System.Net.Sockets;
using System.Text;
using Lumen.Core.Wan.Relay;
using Xunit;

namespace Lumen.Tests;

public class RelayEndToEndTests
{
    [Fact]
    public async Task Http_request_travels_host_relay_client_tunnel()
    {
        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = testCts.Token;

        var secret = "test-secret";
        var relayPort = FreePort();
        var apolloPort = FreePort();

        var relay = new RelayServer(relayPort, secret);
        relay.Start();
        await Task.Delay(100, token);

        var apolloBody = "hello-from-apollo";
        var apollo = new TcpListener(IPAddress.Loopback, apolloPort);
        apollo.Start();
        var apolloTask = ServeApolloAsync(apollo, apolloBody, token);

        try
        {
            using var hostClient = new RelayHostClient("127.0.0.1", relayPort, secret);
            var code = await hostClient.ConnectAsync(token);
            Assert.Matches("^[0-9]{6}$", code);

            using var tunnel = new RelayClientTunnel("127.0.0.1", relayPort, code);
            await tunnel.StartAsync(token);
            var localPort = tunnel.ListenFor(apolloPort);

            using var moonlight = new TcpClient();
            await moonlight.ConnectAsync(IPAddress.Loopback, localPort, token);
            using var ms = moonlight.GetStream();
            var request = Encoding.ASCII.GetBytes("GET / HTTP/1.0\r\n\r\n");
            await ms.WriteAsync(request, token);
            await ms.FlushAsync(token);

            var responseText = await ReadWithTimeoutAsync(ms, apolloBody.Length + 64, token);
            Assert.Contains(apolloBody, responseText);
        }
        finally
        {
            apollo.Stop();
            relay.Stop();
            relay.Dispose();
            try { await apolloTask; } catch { }
        }
    }

    private static async Task<string> ReadWithTimeoutAsync(NetworkStream stream, int minBytes, CancellationToken token)
    {
        var buffer = new byte[4096];
        var total = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (total < minBytes && DateTime.UtcNow < deadline)
        {
            var readTask = stream.ReadAsync(buffer, total, buffer.Length - total, token);
            var done = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(2), token));
            if (done != readTask)
            {
                continue;
            }

            var read = await readTask;
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static async Task ServeApolloAsync(TcpListener listener, string body, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                using (client)
                using (var s = client.GetStream())
                {
                    var buf = new byte[1024];
                    await s.ReadAsync(buf, token);
                    var response = Encoding.ASCII.GetBytes(
                        "HTTP/1.0 200 OK\r\nContent-Length: " + body.Length + "\r\n\r\n" + body);
                    await s.WriteAsync(response, token);
                    await s.FlushAsync(token);
                }
            }, token);
        }
    }

    private static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
