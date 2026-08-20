using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Lumen.Core.Client;

/// <summary>
/// Checks whether THIS Moonlight client is paired with a host by asking /api/serverinfo
/// with the client certificate Moonlight itself uses (so PairStatus is authoritative).
/// Returns null when the client certificate cannot be read (Moonlight never ran).
/// </summary>
public static class MoonlightPairingProbe
{
    public const string RegistryKeyPath = @"Software\Moonlight Game Streaming Project\Moonlight";
    public const string IniFileName = "Moonlight.conf";

    public static async Task<bool?> IsPairedAsync(string host, int port, CancellationToken token)
    {
        var cert = TryLoadClientCertificate();
        if (cert is null)
        {
            return null;
        }

        using var certWithKey = cert;
        using var handler = new HttpClientHandler
        {
            ClientCertificates = { certWithKey },
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        using var http = new HttpClient(handler);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        linked.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var xml = await http.GetStringAsync($"https://{host}:{port}/api/serverinfo", linked.Token)
                .ConfigureAwait(false);
            return ParsePairStatus(xml);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool ParsePairStatus(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var status = doc.Descendants()
                .FirstOrDefault(d => string.Equals(d.Name.LocalName, "PairStatus", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return status == "1";
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static X509Certificate2? TryLoadClientCertificate()
    {
        var pem = TryLoadFromRegistry();
        if (pem is null)
        {
            pem = TryLoadFromPortableIni();
        }

        if (pem is null)
        {
            return null;
        }

        try
        {
            return X509Certificate2.CreateFromPem(pem.Value.Cert, pem.Value.Key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static (string Cert, string Key)? TryLoadFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key is null)
            {
                return null;
            }

            var cert = DecodeQSettingsBlob(key.GetValue("certificate") as byte[]);
            var privateKey = DecodeQSettingsBlob(key.GetValue("key") as byte[]);
            if (cert is null || privateKey is null)
            {
                return null;
            }

            return (cert, privateKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static (string Cert, string Key)? TryLoadFromPortableIni(string? moonlightExe = null)
    {
        var dir = PortableConfigDir(moonlightExe);
        if (dir is null)
        {
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(Path.Combine(dir, IniFileName));
            string? cert = null;
            string? key = null;
            foreach (var line in lines)
            {
                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var name = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (string.Equals(name, "certificate", StringComparison.OrdinalIgnoreCase))
                {
                    cert = DecodeIniByteArray(value);
                }
                else if (string.Equals(name, "key", StringComparison.OrdinalIgnoreCase))
                {
                    key = DecodeIniByteArray(value);
                }
            }

            if (cert is null || key is null)
            {
                return null;
            }

            return (cert, key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Directory where Moonlight keeps its portable Moonlight.conf, or null when not portable.</summary>
    public static string? PortableConfigDir(string? moonlightExe)
    {
        if (string.IsNullOrWhiteSpace(moonlightExe))
        {
            moonlightExe = Lumen.Core.Runtime.ProcessLocator.FindClient()?.Path;
        }

        if (string.IsNullOrWhiteSpace(moonlightExe))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(moonlightExe);
        if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "portable.dat")))
        {
            return null;
        }

        return dir;
    }

    /// <summary>Decodes QSettings' REG_BINARY form (4-byte big-endian length + payload).</summary>
    public static string? DecodeQSettingsBlob(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        if (bytes.Length >= 4)
        {
            var length = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
            if (length == bytes.Length - 4)
            {
                bytes = bytes[4..];
            }
        }

        try
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.Contains("BEGIN", StringComparison.Ordinal) ? text : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Decodes QSettings' INI form: @ByteArray(...) or plain text.</summary>
    public static string? DecodeIniByteArray(string value)
    {
        const string prefix = "@ByteArray(";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            value = value[prefix.Length..^1];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}