using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Lumen.Core.Wan;

public static class StunClient
{
    private const uint MagicCookie = 0x2112A442;

    public static async Task<string?> QueryPublicIpv4Async(
        CancellationToken token,
        string stunHost = "stun.l.google.com",
        int stunPort = 19302)
    {
        try
        {
            var request = BindingRequest();
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 2500;
            await udp.SendAsync(request, request.Length, stunHost, stunPort).WaitAsync(token).ConfigureAwait(false);
            var reply = await udp.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            return TryParseMappedIpv4(reply.Buffer, out var ip) ? ip : null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public static byte[] BindingRequest()
    {
        var message = new byte[20];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(4, 4), MagicCookie);
        RandomNumberGenerator.Fill(message.AsSpan(8, 12));
        return message;
    }

    public static bool TryParseMappedIpv4(ReadOnlySpan<byte> message, out string ip)
    {
        ip = "";
        if (message.Length < 20)
        {
            return false;
        }

        var type = BinaryPrimitives.ReadUInt16BigEndian(message[..2]);
        if (type != 0x0101)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(2, 2));
        var end = Math.Min(message.Length, 20 + length);
        var offset = 20;
        while (offset + 4 <= end)
        {
            var attrType = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
            var attrLen = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset + 2, 2));
            var valueAt = offset + 4;
            if (valueAt + attrLen > message.Length)
            {
                return false;
            }

            var value = message.Slice(valueAt, attrLen);
            if ((attrType is 0x0020 or 0x0001) && TryReadIpv4(value, xor: attrType == 0x0020, out ip))
            {
                return true;
            }

            offset = valueAt + ((attrLen + 3) & ~3);
        }

        return false;
    }

    private static bool TryReadIpv4(ReadOnlySpan<byte> value, bool xor, out string ip)
    {
        ip = "";
        if (value.Length < 8 || value[1] != 0x01)
        {
            return false;
        }

        var port = BinaryPrimitives.ReadUInt16BigEndian(value.Slice(2, 2));
        var b0 = value[4];
        var b1 = value[5];
        var b2 = value[6];
        var b3 = value[7];
        if (xor)
        {
            port ^= 0x2112;
            b0 ^= 0x21;
            b1 ^= 0x12;
            b2 ^= 0xA4;
            b3 ^= 0x42;
        }

        _ = port;
        ip = new IPAddress([b0, b1, b2, b3]).ToString();
        return true;
    }
}
