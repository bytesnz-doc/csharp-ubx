using System.Runtime.CompilerServices;

namespace CSharpUbx;

public sealed class UbxClient
{
    private readonly Stream _stream;
    private readonly UbxParser _parser = new();

    public UbxClient(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>
    /// Builds a framed UBX message and writes it to the stream.
    /// </summary>
    public async Task SendAsync(
        UbxClass msgClass,
        byte msgId,
        ReadOnlyMemory<byte> payload = default,
        CancellationToken cancellationToken = default)
    {
        if (!_stream.CanWrite)
        {
            throw new InvalidOperationException("Stream must be writable.");
        }

        var frame = BuildFrame((byte)msgClass, msgId, payload.Span);
        await _stream.WriteAsync(frame, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Sends a configuration message and waits for an ACK or NAK response.
    /// Returns <see langword="true"/> on ACK, <see langword="false"/> on NAK.
    /// Pass a <see cref="CancellationToken"/> from a <see cref="System.Threading.CancellationTokenSource"/>
    /// with <c>CancelAfter</c> to enforce a timeout.
    /// </summary>
    public async Task<bool> SendConfigAsync(
        UbxClass msgClass,
        byte msgId,
        ReadOnlyMemory<byte> payload = default,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(msgClass, msgId, payload, cancellationToken);

        if (!_stream.CanRead)
        {
            throw new InvalidOperationException("Stream must be readable to wait for ACK/NAK.");
        }

        var readBuffer = new byte[256];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesRead = await _stream.ReadAsync(readBuffer, cancellationToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Stream ended while waiting for ACK/NAK.");
            }

            var messages = _parser.Feed(readBuffer.AsSpan(0, bytesRead));
            foreach (var msg in messages)
            {
                if (msg.MessageClass == (byte)UbxClass.Ack &&
                    msg.Payload.Length == 2 &&
                    msg.Payload.Span[0] == (byte)msgClass &&
                    msg.Payload.Span[1] == msgId)
                {
                    return msg.MessageId == (byte)AckMessageId.Ack;
                }
            }
        }
    }

    /// <summary>
    /// Reads UBX messages from the stream indefinitely, verifying checksums and
    /// extracting payloads. Yields each valid message as it arrives.
    /// </summary>
    public async IAsyncEnumerable<UbxMessage> ReadMessagesAsync(
        int bufferSize = 1024,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }

        if (!_stream.CanRead)
        {
            throw new InvalidOperationException("Stream must be readable.");
        }

        var readBuffer = new byte[bufferSize];

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, bufferSize), cancellationToken);
            if (bytesRead == 0)
            {
                yield break;
            }

            var messages = _parser.Feed(readBuffer.AsSpan(0, bytesRead));
            foreach (var message in messages)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Builds a complete UBX frame (sync chars, header, payload, checksum) from
    /// its component parts without sending it.
    /// </summary>
    public static byte[] BuildFrame(byte msgClass, byte msgId, ReadOnlySpan<byte> payload)
    {
        var length = (ushort)payload.Length;
        var frame = new byte[8 + payload.Length];
        frame[0] = UbxMessage.SyncChar1;
        frame[1] = UbxMessage.SyncChar2;
        frame[2] = msgClass;
        frame[3] = msgId;
        frame[4] = (byte)(length & 0xFF);
        frame[5] = (byte)(length >> 8);
        payload.CopyTo(frame.AsSpan(6));
        var ck = UbxChecksum.Compute(frame.AsSpan(2, 4 + payload.Length));
        frame[6 + payload.Length] = ck.CkA;
        frame[7 + payload.Length] = ck.CkB;
        return frame;
    }
}
