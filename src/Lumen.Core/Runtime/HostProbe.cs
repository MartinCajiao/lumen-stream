using System.Net.Sockets;
using Lumen.Core.Wan;

namespace Lumen.Core.Runtime;

public sealed record HostProbeResult(bool Reachable, string Message);

public static class HostProbe
{
    public static string ExplainUnreachable(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "No hay código. En el PC gamer pulsa Compartir y cópialo.";
        }

        if (NetworkAddresses.IsTailscaleIpv4(address))
        {
            return "Ese código es Tailscale (100.x). En ESTE PC también tiene que estar Tailscale instalado y con la misma cuenta.";
        }

        if (NetworkAddresses.IsPrivateIpv4(address))
        {
            return "Esa IP es de la wifi de la otra casa. Desde otra red no existe. Instala Tailscale (gratis) en las dos PCs, vuelve a compartir, y usa el código 100.x.";
        }

        return "No llega al PC gamer. El router no abrió puertos o tu internet es CGNAT. Lo que sí funciona entre dos casas: Tailscale en las dos PCs.";
    }

    public static async Task<HostProbeResult> CheckAsync(string address, int port, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return new HostProbeResult(false, ExplainUnreachable(address));
        }

        try
        {
            using var client = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            linked.CancelAfter(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(address, port, linked.Token).ConfigureAwait(false);
            return new HostProbeResult(true, "El otro PC responde.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return new HostProbeResult(false, ExplainUnreachable(address));
        }
        catch (SocketException)
        {
            return new HostProbeResult(false, ExplainUnreachable(address));
        }
        catch (ArgumentException)
        {
            return new HostProbeResult(false, "Ese código no es una IP. Copia el que sale al compartir.");
        }
    }
}
