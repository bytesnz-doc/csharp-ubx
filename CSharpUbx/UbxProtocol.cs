// UbxProtocol.cs
// Drop this single file into any project to communicate with u-blox UBX devices over UART.
// Protocol reference: u-blox M10 SPG 5.30 Interface Description (UBXDOC-304424225-20395)

using System.Runtime.CompilerServices;

namespace CSharpUbx;

// ─────────────────────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────────────────────

/// <summary>UBX message class identifiers.</summary>
public enum UbxClass : byte
{
    Nav = 0x01,
    Rxm = 0x02,
    Inf = 0x04,
    Ack = 0x05,
    Cfg = 0x06,
    Mon = 0x0A,
    Tim = 0x0D,
    Sec = 0x27,
}

/// <summary>Message IDs within the ACK class.</summary>
public enum AckMessageId : byte
{
    Nak = 0x00,
    Ack = 0x01,
}

/// <summary>Message IDs within the CFG class.</summary>
public enum CfgMessageId : byte
{
    Rst    = 0x04,
    Valset = 0x8A,
}

// ─────────────────────────────────────────────────────────────
// Core message type
// ─────────────────────────────────────────────────────────────

/// <summary>A parsed, checksum-verified UBX message.</summary>
public readonly record struct UbxMessage(byte MessageClass, byte MessageId, ReadOnlyMemory<byte> Payload)
{
    public const byte SyncChar1 = 0xB5;
    public const byte SyncChar2 = 0x62;
}

// ─────────────────────────────────────────────────────────────
// Checksum  (Fletcher-8 as defined by the UBX specification)
// ─────────────────────────────────────────────────────────────

/// <summary>Computes the UBX Fletcher-8 checksum.</summary>
public static class UbxChecksum
{
    public static (byte CkA, byte CkB) Compute(ReadOnlySpan<byte> data)
    {
        byte ckA = 0, ckB = 0;
        foreach (var value in data)
        {
            unchecked
            {
                ckA += value;
                ckB += ckA;
            }
        }
        return (ckA, ckB);
    }
}

// ─────────────────────────────────────────────────────────────
// Incremental frame parser
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Stateful parser that accepts arbitrary byte chunks and emits complete,
/// checksum-verified <see cref="UbxMessage"/> instances.
/// </summary>
public sealed class UbxParser
{
    private readonly List<byte> _buffer = [];

    /// <summary>
    /// Feed raw bytes from the stream into the parser.
    /// Returns all complete messages found in the accumulated buffer.
    /// Frames with invalid checksums are silently discarded.
    /// </summary>
    public IReadOnlyList<UbxMessage> Feed(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            _buffer.Add(data[i]);
        }

        var messages = new List<UbxMessage>();
        var cursor = 0;

        while (cursor + 8 <= _buffer.Count)
        {
            if (_buffer[cursor] != UbxMessage.SyncChar1 || _buffer[cursor + 1] != UbxMessage.SyncChar2)
            {
                cursor++;
                continue;
            }

            var payloadLength = (ushort)(_buffer[cursor + 4] | (_buffer[cursor + 5] << 8));
            var frameLength = 8 + (int)payloadLength;

            if (cursor + frameLength > _buffer.Count)
            {
                break;
            }

            var classIdLenPayload = _buffer.GetRange(cursor + 2, 4 + payloadLength).ToArray();
            var checksum = UbxChecksum.Compute(classIdLenPayload);
            var ckA = _buffer[cursor + 6 + payloadLength];
            var ckB = _buffer[cursor + 7 + payloadLength];

            if (checksum.CkA == ckA && checksum.CkB == ckB)
            {
                var payload = _buffer.GetRange(cursor + 6, payloadLength).ToArray();
                messages.Add(new UbxMessage(_buffer[cursor + 2], _buffer[cursor + 3], payload));
                cursor += frameLength;
            }
            else
            {
                cursor++;
            }
        }

        if (cursor > 0)
        {
            _buffer.RemoveRange(0, cursor);
        }

        return messages;
    }
}

// ─────────────────────────────────────────────────────────────
// UBX client  (send / send-config / receive)
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Main entry point for UBX communication over any bidirectional <see cref="Stream"/>
/// (e.g. a serial port's BaseStream).
/// </summary>
public sealed class UbxClient
{
    private readonly Stream _stream;
    private readonly UbxParser _parser = new();
    private readonly Queue<UbxMessage> _pendingMessages = new();

    public UbxClient(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>
    /// Builds a complete UBX frame and writes it to the stream.
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
    /// Sends a configuration message and blocks until the device replies with
    /// ACK or NAK for that specific class/id.
    /// Returns <see langword="true"/> on ACK, <see langword="false"/> on NAK.
    /// <para>
    /// Tip: pass a <c>CancellationToken</c> from
    /// <c>new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token</c>
    /// to enforce a receive timeout.
    /// </para>
    /// </summary>
    public async Task<bool> SendConfigAsync(
        UbxClass msgClass,
        byte msgId,
        ReadOnlyMemory<byte> payload = default,
        CancellationToken cancellationToken = default)
    {
        if (!_stream.CanRead)
        {
            throw new InvalidOperationException("Stream must be readable to wait for ACK/NAK.");
        }

        await SendAsync(msgClass, msgId, payload, cancellationToken);

        var readBuffer = new byte[256];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Always enqueue a fresh batch before draining, so we never lose
            // messages that arrive alongside our ACK/NAK in the same read.
            if (_pendingMessages.Count == 0)
            {
                var bytesRead = await _stream.ReadAsync(readBuffer, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Stream ended while waiting for ACK/NAK.");
                }

                foreach (var msg in _parser.Feed(readBuffer.AsSpan(0, bytesRead)))
                {
                    _pendingMessages.Enqueue(msg);
                }
            }

            while (_pendingMessages.Count > 0)
            {
                var msg = _pendingMessages.Dequeue();
                if (IsAckNakFor(msg, msgClass, msgId))
                {
                    return msg.MessageId == (byte)AckMessageId.Ack;
                }
            }
        }
    }

    /// <summary>
    /// Continuously reads from the stream and yields each checksum-verified
    /// <see cref="UbxMessage"/> as it arrives.
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
            // Yield any messages buffered from a prior SendConfigAsync call.
            while (_pendingMessages.Count > 0)
            {
                yield return _pendingMessages.Dequeue();
            }

            var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, bufferSize), cancellationToken);
            if (bytesRead == 0)
            {
                yield break;
            }

            foreach (var message in _parser.Feed(readBuffer.AsSpan(0, bytesRead)))
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Builds a raw UBX frame byte array without sending it.
    /// Useful for pre-computing frames or unit testing.
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

    private static bool IsAckNakFor(UbxMessage msg, UbxClass msgClass, byte msgId) =>
        msg.MessageClass == (byte)UbxClass.Ack &&
        msg.Payload.Length == 2 &&
        msg.Payload.Span[0] == (byte)msgClass &&
        msg.Payload.Span[1] == msgId;
}

// ─────────────────────────────────────────────────────────────
// M10-specific: typed command record
// ─────────────────────────────────────────────────────────────

/// <summary>A pre-defined UBX command with its class, message id and payload.</summary>
public readonly record struct UbxCommand(UbxClass Class, byte MessageId, byte[] Payload);

// ─────────────────────────────────────────────────────────────
// M10-specific: command catalogue
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Pre-built CFG-VALSET and CFG-RST command payloads for the u-blox M10 module.
/// </summary>
public static class M10Commands
{
    /// <summary>CFG-VALSET: enable GPS constellation.</summary>
    public static readonly UbxCommand GpsEnable =
        Valset("000100001f00311001");

    /// <summary>CFG-VALSET: disable Galileo constellation.</summary>
    public static readonly UbxCommand GalileoDisable =
        Valset("000100002100311000");

    /// <summary>CFG-VALSET: disable BDS constellation.</summary>
    public static readonly UbxCommand BdsDisable =
        Valset("000100002200311000");

    /// <summary>CFG-VALSET: disable GLONASS constellation.</summary>
    public static readonly UbxCommand GlonassDisable =
        Valset("000100002500311000");

    /// <summary>CFG-RST: simulate GNSS cold start.</summary>
    public static readonly UbxCommand CfgRstColdStart =
        new(UbxClass.Cfg, (byte)CfgMessageId.Rst, Convert.FromHexString("ffff0100"));

    private static UbxCommand Valset(string payloadHex) =>
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, Convert.FromHexString(payloadHex));
}

// ─────────────────────────────────────────────────────────────
// M10-specific: configurator
// ─────────────────────────────────────────────────────────────

/// <summary>
/// High-level helper that configures an M10 module for GPS-only CloudLocate operation.
/// </summary>
public static class M10Configurator
{
    /// <summary>
    /// Configures GPS-only constellation and enables the chosen raw measurement output.
    /// Each CFG-VALSET is sent and an ACK is awaited before proceeding to the next command.
    /// Throws <see cref="InvalidOperationException"/> if any command is NAK'd.
    /// </summary>
    public static async Task ConfigureGpsOnlyAsync(
        UbxClient client,
        CancellationToken cancellationToken = default)
    {
        await SendAndVerifyAsync(client, M10Commands.GpsEnable, cancellationToken);
        await SendAndVerifyAsync(client, M10Commands.GalileoDisable, cancellationToken);
        await SendAndVerifyAsync(client, M10Commands.BdsDisable, cancellationToken);
        await SendAndVerifyAsync(client, M10Commands.GlonassDisable, cancellationToken);
    }

    /// <summary>
    /// Sends UBX-CFG-RST to simulate a GNSS cold start.
    /// CFG-RST does not produce an ACK (the device resets immediately).
    /// </summary>
    public static Task TriggerColdStartResetAsync(
        UbxClient client,
        CancellationToken cancellationToken = default)
    {
        var cmd = M10Commands.CfgRstColdStart;
        return client.SendAsync(cmd.Class, cmd.MessageId, cmd.Payload, cancellationToken);
    }

    private static async Task SendAndVerifyAsync(UbxClient client, UbxCommand cmd, CancellationToken ct)
    {
        var ack = await client.SendConfigAsync(cmd.Class, cmd.MessageId, cmd.Payload, ct);
        if (!ack)
        {
            throw new InvalidOperationException(
                $"NAK received for command class=0x{(byte)cmd.Class:X2} id=0x{cmd.MessageId:X2}.");
        }
    }
}

