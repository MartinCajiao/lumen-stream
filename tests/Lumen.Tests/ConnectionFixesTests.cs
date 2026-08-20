using System.Text;
using Lumen.Core.Client;
using Lumen.Core.Runtime;
using Xunit;

namespace Lumen.Tests;

public sealed class ConnectionFixesTests
{
    [Fact]
    public void Pair_status_one_means_paired()
    {
        Assert.True(MoonlightPairingProbe.ParsePairStatus("<?xml version=\"1.0\"?><root><serverinfo><PairStatus>1</PairStatus></serverinfo></root>"));
        Assert.False(MoonlightPairingProbe.ParsePairStatus("<?xml version=\"1.0\"?><root><serverinfo><PairStatus>0</PairStatus></serverinfo></root>"));
        Assert.False(MoonlightPairingProbe.ParsePairStatus("not xml"));
    }

    [Fact]
    public void Pair_status_is_case_insensitive()
    {
        Assert.True(MoonlightPairingProbe.ParsePairStatus("<root><pairstatus>1</pairstatus></root>"));
    }

    [Fact]
    public void QSettings_registry_blob_decodes_to_pem()
    {
        var pem = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----";
        var bytes = Encoding.UTF8.GetBytes(pem);
        var blob = new byte[4 + bytes.Length];
        blob[0] = (byte)(bytes.Length >> 24);
        blob[1] = (byte)(bytes.Length >> 16);
        blob[2] = (byte)(bytes.Length >> 8);
        blob[3] = (byte)bytes.Length;
        bytes.CopyTo(blob, 4);

        var decoded = MoonlightPairingProbe.DecodeQSettingsBlob(blob);
        Assert.NotNull(decoded);
        Assert.Contains("BEGIN CERTIFICATE", decoded);
    }

    [Fact]
    public void QSettings_ini_byte_array_decodes()
    {
        Assert.Equal("PEM DATA", MoonlightPairingProbe.DecodeIniByteArray("@ByteArray(PEM DATA)"));
        Assert.Equal("PEM DATA", MoonlightPairingProbe.DecodeIniByteArray("PEM DATA"));
        Assert.Null(MoonlightPairingProbe.DecodeIniByteArray(""));
    }

    [Fact]
    public void Portable_config_dir_detects_portable_dat()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-portable-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var exe = Path.Combine(dir, "Moonlight.exe");
            File.WriteAllText(exe, "");
            Assert.Null(MoonlightPairingProbe.PortableConfigDir(exe));

            File.WriteAllText(Path.Combine(dir, "portable.dat"), "");
            Assert.Equal(dir, MoonlightPairingProbe.PortableConfigDir(exe));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Netstat_listening_line_yields_pid()
    {
        Assert.True(PortConflictProbe.TryParseListeningLine("  TCP    0.0.0.0:47989           0.0.0.0:0              LISTENING       1234", 47989, out var pid));
        Assert.Equal(1234, pid);
        Assert.False(PortConflictProbe.TryParseListeningLine("  TCP    0.0.0.0:47989           0.0.0.0:0              LISTENING       1234", 48000, out _));
        Assert.False(PortConflictProbe.TryParseListeningLine("  TCP    0.0.0.0:47989           192.168.1.4:52341       ESTABLISHED     1234", 47989, out _));
        Assert.False(PortConflictProbe.TryParseListeningLine("garbage", 47989, out _));
    }
}