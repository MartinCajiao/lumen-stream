using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Lumen.Core.Paths;

namespace Lumen.Core.Host;

/// <summary>PIN pairing against Apollo/Sunshine HTTPS UI (localhost:47990).</summary>
public static class PairingClient
{
    public static string PasswordFor(string user) => user.Trim() + "-host";

    public static HttpClient CreateLocalClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = AllowLocalSunshine,
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
    }

    public static async Task<bool> SubmitPinAsync(string pin, string clientName, CancellationToken token)
    {
        var user = string.IsNullOrWhiteSpace(clientName) ? "lumen" : clientName.Trim();
        using var http = CreateLocalClient();
        var login = await http.PostAsJsonAsync(
            "https://localhost:47990/api/login",
            new { username = user, password = PasswordFor(user) },
            token).ConfigureAwait(false);
        if (!login.IsSuccessStatusCode)
        {
            return false;
        }

        var response = await http.PostAsJsonAsync(
            "https://localhost:47990/api/pin",
            new { pin, name = user },
            token).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public static bool CredentialsFileLooksReady() =>
        File.Exists(LumenPaths.HostCredentialsFile);

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
