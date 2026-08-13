using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Lumen.Core.Host;

/// <summary>PIN pairing against Apollo/Sunshine HTTPS UI (localhost:47990).</summary>
public static class PairingClient
{
    public static HttpClient CreateLocalClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = AllowLocalSunshine
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    public static async Task<bool> SubmitPinAsync(string pin, string clientName, CancellationToken token)
    {
        using var http = CreateLocalClient();
        var response = await http.PostAsJsonAsync(
            "https://localhost:47990/api/pin",
            new { pin, name = clientName },
            token).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private static bool AllowLocalSunshine(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        var host = request.RequestUri?.Host;
        return host is "localhost" or "127.0.0.1" or "::1";
    }
}
