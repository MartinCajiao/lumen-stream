using System.Net.Sockets;

namespace Lumen.Core.Wan.Relay;

/// <summary>
/// Length-prefixed multiplexed relay protocol. One TCP control connection per peer
/// carries many streams (GameStream uses several TCP + UDP ports). Framing:
///   [1 byte type][4-byte sessionId BE][4-byte payload length BE][payload]
/// This lets a single public port on the relay serve every GameStream port, so the
/// relay VM only needs one inbound port — easy to deploy on a free cloud VM.
/// </summary>
public static class RelayFrame
{
    public const byte Open = 0x01;     // client -> relay -> host: open a stream to a local port
    public const byte Data = 0x02;    // either direction: stream bytes
    public const byte Close = 0x03;   // either direction: close a stream
    public const byte Opened = 0x04;  // host -> relay -> client: stream connected
    public const byte Failed = 0x05;   // host -> relay -> client: stream could not open

    public const int HeaderSize = 9;

    public static byte[] Encode(byte type, int sessionId, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[HeaderSize + payload.Length];
        frame[0] = type;
        BinaryPrimitivesWriteInt32BE(frame, 1, sessionId);
        BinaryPrimitivesWriteInt32BE(frame, 5, payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    /// <summary>Reads one full frame from the stream. Returns null on clean EOF.</summary>
    public static async Task<(byte Type, int SessionId, byte[] Payload)?> ReadAsync(
        NetworkStream stream,
        CancellationToken token)
    {
        var header = await ReadExactAsync(stream, HeaderSize, token).ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }

        var type = header[0];
        var sessionId = BinaryPrimitivesReadInt32BE(header, 1);
        var length = BinaryPrimitivesReadInt32BE(header, 5);
        if (length < 0 || length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException($"Relay frame too large: {length}");
        }

        byte[] payload;
        if (length == 0)
        {
            payload = Array.Empty<byte>();
        }
        else
        {
            var body = await ReadExactAsync(stream, length, token).ConfigureAwait(false);
            payload = body ?? throw new EndOfStreamException("Relay peer closed mid-frame");
        }

        return (type, sessionId, payload);
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int count, CancellationToken token)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), token)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? null : throw new EndOfStreamException("Relay peer closed mid-header");
            }

            offset += read;
        }

        return buffer;
    }

    private static void BinaryPrimitivesWriteInt32BE(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static int BinaryPrimitivesReadInt32BE(byte[] buf, int offset)
        => (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];
}
