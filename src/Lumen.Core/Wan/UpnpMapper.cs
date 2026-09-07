using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lumen.Core.Wan;

public static class UpnpMapper
{
    public static string AddPortMappingEnvelope(string serviceType, int port, string protocol, string internalIp) =>
        $"""
        <?xml version="1.0"?>
        <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
          <s:Body>
            <u:AddPortMapping xmlns:u="{serviceType}">
              <NewRemoteHost></NewRemoteHost>
              <NewExternalPort>{port}</NewExternalPort>
              <NewProtocol>{protocol}</NewProtocol>
              <NewInternalPort>{port}</NewInternalPort>
              <NewInternalClient>{internalIp}</NewInternalClient>
              <NewEnabled>1</NewEnabled>
              <NewPortMappingDescription>Lumen</NewPortMappingDescription>
              <NewLeaseDuration>0</NewLeaseDuration>
            </u:AddPortMapping>
          </s:Body>
        </s:Envelope>
        """;

    public static string? TryParseSsdpLocation(string message)
    {
        var match = Regex.Match(message, @"LOCATION:\s*(\S+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static string CombineUrl(string deviceUrl, string controlUrl)
    {
        if (controlUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return controlUrl;
        }

        var baseUri = new Uri(deviceUrl);
        return new Uri(baseUri, controlUrl).ToString();
    }

    public static async Task<int> MapAsync(string internalIp, int httpPort, CancellationToken token)
    {
        var mapped = 0;
        var igd = await FindGatewayAsync(token).ConfigureAwait(false);
        if (igd is null)
        {
            return 0;
        }

        foreach (var port in GameStreamPorts.ForBase(httpPort))
        {
            token.ThrowIfCancellationRequested();
            if (await AddMappingAsync(igd.Value.ControlUrl, igd.Value.ServiceType, port.Number, port.Protocol, internalIp, token)
                    .ConfigureAwait(false))
            {
                mapped++;
            }
        }

        return mapped;
    }

    private static async Task<(string ControlUrl, string ServiceType)?> FindGatewayAsync(CancellationToken token)
    {
        var locations = await DiscoverLocationsAsync(token).ConfigureAwait(false);
        foreach (var location in locations)
        {
            var igd = await ReadControlUrlAsync(location, token).ConfigureAwait(false);
            if (igd is not null)
            {
                return igd;
            }
        }

        return null;
    }

    /// <summary>Asks the router for its WAN IP (GetExternalIPAddress). Null when no UPnP gateway answers.</summary>
    public static async Task<string?> QueryRouterWanIpAsync(CancellationToken token)
    {
        var igd = await FindGatewayAsync(token).ConfigureAwait(false);
        if (igd is null)
        {
            return null;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            using var request = new HttpRequestMessage(HttpMethod.Post, igd.Value.ControlUrl);
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{igd.Value.ServiceType}#GetExternalIPAddress\"");
            request.Content = new StringContent(
                $"""
                <?xml version="1.0"?>
                <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
                  <s:Body>
                    <u:GetExternalIPAddress xmlns:u="{igd.Value.ServiceType}"/>
                  </s:Body>
                </s:Envelope>
                """,
                Encoding.UTF8,
                "text/xml");
            using var response = await http.SendAsync(request, token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            var match = Regex.Match(body, @"<NewExternalIPAddress>([^<]+)</NewExternalIPAddress>", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<List<string>> DiscoverLocationsAsync(CancellationToken token)
    {
        var found = new List<string>();
            var request = Encoding.ASCII.GetBytes(
                "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n\r\n");

        try
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 1500;
            await udp.SendAsync(request, request.Length, "239.255.255.250", 1900).WaitAsync(token).ConfigureAwait(false);
            var until = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (DateTime.UtcNow < until)
            {
                token.ThrowIfCancellationRequested();
                var remaining = until - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var receive = udp.ReceiveAsync();
                var done = await Task.WhenAny(receive, Task.Delay(remaining, token)).ConfigureAwait(false);
                if (done != receive)
                {
                    break;
                }

                var reply = await receive.ConfigureAwait(false);
                var text = Encoding.ASCII.GetString(reply.Buffer);
                var location = TryParseSsdpLocation(text);
                if (location is not null && !found.Contains(location, StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(location);
                }
            }
        }
        catch (SocketException)
        {
        }
        catch (TimeoutException)
        {
        }

        return found;
    }

    private static async Task<(string ControlUrl, string ServiceType)?> ReadControlUrlAsync(string deviceUrl, CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var xml = await http.GetStringAsync(deviceUrl, token).ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace ns = "urn:schemas-upnp-org:device-1-0";
            foreach (var service in doc.Descendants(ns + "service"))
            {
                var type = service.Element(ns + "serviceType")?.Value ?? "";
                if (!type.Contains("WANIPConnection", StringComparison.OrdinalIgnoreCase)
                    && !type.Contains("WANPPPConnection", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var control = service.Element(ns + "controlURL")?.Value;
                if (string.IsNullOrWhiteSpace(control))
                {
                    continue;
                }

                return (CombineUrl(deviceUrl, control), type);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static async Task<bool> AddMappingAsync(
        string controlUrl,
        string serviceType,
        int port,
        string protocol,
        string internalIp,
        CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            using var request = new HttpRequestMessage(HttpMethod.Post, controlUrl);
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{serviceType}#AddPortMapping\"");
            request.Content = new StringContent(
                AddPortMappingEnvelope(serviceType, port, protocol, internalIp),
                Encoding.UTF8,
                "text/xml");
            using var response = await http.SendAsync(request, token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
