using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Lumen.Core.Paths;

namespace Lumen.Core.Host;

/// <summary>PIN pairing against Apollo/Sunshine HTTPS UI (localhost:47990).</summary>
public static class PairingClient
{
    /// <summary>
    /// Fixed PIN Lumen uses to auto-pair two Lumen PCs without anyone typing a PIN.
    /// The host auto-approves this PIN while sharing; the client sends it with
    /// <c>moonlight pair &lt;host&gt; --pin 1234</c>. For a personal LAN streaming
    /// tool this is the friction-free path the user asked for.
    /// </summary>
    public const string LumenAutoPin = "1234";

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

    /// <summary>
    /// Submits a PIN to the local Apollo. Returns true only when Apollo actually
    /// completed a pairing (the response body's <c>status</c> is true), false when
    /// there was no pending client or the PIN didn't match.
    /// </summary>
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
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        try
        {
            var body = await response.Content.ReadFromJsonAsync<PinResponse>(token).ConfigureAwait(false);
            return body?.Status == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs on the HOST while sharing. Polls <c>/api/pin</c> with the fixed Lumen
    /// PIN so any Lumen client that connects with <c>--pin 1234</c> gets paired
    /// automatically — no one types a PIN. Stops when the token cancels (host
    /// stopped sharing). Returns the number of clients it paired.
    /// </summary>
    public static async Task<int> AutoApproveLoopAsync(string clientName, CancellationToken token)
    {
        var paired = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await SubmitPinAsync(LumenAutoPin, clientName, token).ConfigureAwait(false))
                {
                    paired++;
                    // A client just paired. Back off a couple seconds so we don't
                    // hammer the API, then keep watching for more clients.
                    await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                    continue;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Apollo not ready yet (still starting). Try again next tick.
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return paired;
    }

    public static bool CredentialsFileLooksReady() =>
        File.Exists(LumenPaths.HostCredentialsFile);

    private sealed class PinResponse
    {
        public bool? Status { get; set; }
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
