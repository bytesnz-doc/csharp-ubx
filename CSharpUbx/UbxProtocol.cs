using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpUbx
{
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
        Rst = 0x04,
        Valset = 0x8A,
    }

    /// <summary>A parsed, checksum-verified UBX message.</summary>
    public sealed class UbxMessage
    {
        public const byte SyncChar1 = 0xB5;
        public const byte SyncChar2 = 0x62;

        public UbxMessage(byte messageClass, byte messageId, byte[] payload)
        {
            MessageClass = messageClass;
            MessageId = messageId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public byte MessageClass { get; private set; }

        public byte MessageId { get; private set; }

        public byte[] Payload { get; private set; }
    }

    /// <summary>Computes the UBX Fletcher-8 checksum.</summary>
    public static class UbxChecksum
    {
        public static Tuple<byte, byte> Compute(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte ckA = 0;
            byte ckB = 0;

            for (var i = 0; i < count; i++)
            {
                unchecked
                {
                    ckA += data[offset + i];
                    ckB += ckA;
                }
            }

            return Tuple.Create(ckA, ckB);
        }
    }

    /// <summary>
    /// Stateful parser that accepts arbitrary byte chunks and emits complete,
    /// checksum-verified UbxMessage instances.
    /// </summary>
    public sealed class UbxParser
    {
        private readonly List<byte> _buffer = new List<byte>();

        /// <summary>
        /// Feed raw bytes from the stream into the parser.
        /// Returns all complete messages found in the accumulated buffer.
        /// Frames with invalid checksums are silently discarded.
        /// </summary>
        public IReadOnlyList<UbxMessage> Feed(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Feed(data, 0, data.Length);
        }

        /// <summary>
        /// Feed a segment of raw bytes from the stream into the parser.
        /// </summary>
        public IReadOnlyList<UbxMessage> Feed(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (var i = 0; i < count; i++)
            {
                _buffer.Add(data[offset + i]);
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
                var frameLength = 8 + payloadLength;

                if (cursor + frameLength > _buffer.Count)
                {
                    break;
                }

                var classIdLenPayload = _buffer.GetRange(cursor + 2, 4 + payloadLength).ToArray();
                var checksum = UbxChecksum.Compute(classIdLenPayload, 0, classIdLenPayload.Length);
                var ckA = _buffer[cursor + 6 + payloadLength];
                var ckB = _buffer[cursor + 7 + payloadLength];

                if (checksum.Item1 == ckA && checksum.Item2 == ckB)
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

    /// <summary>
    /// Main entry point for UBX communication over any bidirectional Stream
    /// (e.g. a serial port's BaseStream).
    /// </summary>
    public sealed class UbxClient
    {
        private readonly Stream _stream;
        private readonly UbxParser _parser = new UbxParser();
        private readonly Queue<UbxMessage> _pendingMessages = new Queue<UbxMessage>();

        public UbxClient(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _stream = stream;
        }

        /// <summary>
        /// Builds a complete UBX frame and writes it to the stream.
        /// </summary>
        public async Task SendAsync(
            UbxClass msgClass,
            byte msgId,
            byte[] payload = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_stream.CanWrite)
            {
                throw new InvalidOperationException("Stream must be writable.");
            }

            payload = payload ?? new byte[0];
            var frame = BuildFrame((byte)msgClass, msgId, payload);
            await _stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a configuration message and waits until matching ACK or NAK is received.
        /// Returns true for ACK, false for NAK.
        /// </summary>
        public async Task<bool> SendConfigAsync(
            UbxClass msgClass,
            byte msgId,
            byte[] payload = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_stream.CanRead)
            {
                throw new InvalidOperationException("Stream must be readable to wait for ACK/NAK.");
            }

            await SendAsync(msgClass, msgId, payload, cancellationToken).ConfigureAwait(false);

            var readBuffer = new byte[256];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_pendingMessages.Count == 0)
                {
                    var bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException("Stream ended while waiting for ACK/NAK.");
                    }

                    var parsed = _parser.Feed(readBuffer, 0, bytesRead);
                    for (var i = 0; i < parsed.Count; i++)
                    {
                        _pendingMessages.Enqueue(parsed[i]);
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
        /// Reads one chunk from the stream and returns all checksum-verified messages found.
        /// </summary>
        public async Task<IReadOnlyList<UbxMessage>> ReceiveMessagesAsync(
            int bufferSize,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            if (!_stream.CanRead)
            {
                throw new InvalidOperationException("Stream must be readable.");
            }

            var result = new List<UbxMessage>();
            while (_pendingMessages.Count > 0)
            {
                result.Add(_pendingMessages.Dequeue());
            }

            var readBuffer = new byte[bufferSize];
            var bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return result;
            }

            var parsed = _parser.Feed(readBuffer, 0, bytesRead);
            for (var i = 0; i < parsed.Count; i++)
            {
                result.Add(parsed[i]);
            }

            return result;
        }

        /// <summary>
        /// Builds a raw UBX frame byte array without sending it.
        /// </summary>
        public static byte[] BuildFrame(byte msgClass, byte msgId, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(payload), "Payload cannot exceed 65535 bytes.");
            }

            var length = (ushort)payload.Length;
            var frame = new byte[8 + payload.Length];
            frame[0] = UbxMessage.SyncChar1;
            frame[1] = UbxMessage.SyncChar2;
            frame[2] = msgClass;
            frame[3] = msgId;
            frame[4] = (byte)(length & 0xFF);
            frame[5] = (byte)(length >> 8);

            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, 6, payload.Length);
            }

            var checksum = UbxChecksum.Compute(frame, 2, 4 + payload.Length);
            frame[6 + payload.Length] = checksum.Item1;
            frame[7 + payload.Length] = checksum.Item2;
            return frame;
        }

        private static bool IsAckNakFor(UbxMessage msg, UbxClass msgClass, byte msgId)
        {
            return msg.MessageClass == (byte)UbxClass.Ack &&
                   msg.Payload.Length == 2 &&
                   msg.Payload[0] == (byte)msgClass &&
                   msg.Payload[1] == msgId;
        }
    }

    /// <summary>A pre-defined UBX command with class, message id and payload.</summary>
    public sealed class UbxCommand
    {
        public UbxCommand(UbxClass messageClass, byte messageId, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            Class = messageClass;
            MessageId = messageId;
            Payload = payload;
        }

        public UbxClass Class { get; private set; }

        public byte MessageId { get; private set; }

        public byte[] Payload { get; private set; }
    }

    /// <summary>Pre-built CFG-VALSET and CFG-RST command payloads for u-blox M10.</summary>
    public static class M10Commands
    {
        public static readonly UbxCommand GpsEnable = Valset("000100001f00311001");
        public static readonly UbxCommand GalileoDisable = Valset("000100002100311000");
        public static readonly UbxCommand BdsDisable = Valset("000100002200311000");
        public static readonly UbxCommand GlonassDisable = Valset("000100002500311000");
        public static readonly UbxCommand CfgRstColdStart = new UbxCommand(UbxClass.Cfg, (byte)CfgMessageId.Rst, HexToBytes("ffff0100"));

        private static UbxCommand Valset(string payloadHex)
        {
            return new UbxCommand(UbxClass.Cfg, (byte)CfgMessageId.Valset, HexToBytes(payloadHex));
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            if ((hex.Length % 2) != 0)
            {
                throw new FormatException("Hex string length must be even.");
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var high = ParseHexNibble(hex[i * 2]);
                var low = ParseHexNibble(hex[(i * 2) + 1]);
                bytes[i] = (byte)((high << 4) | low);
            }

            return bytes;
        }

        private static int ParseHexNibble(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            throw new FormatException("Invalid hex character: " + c);
        }
    }

    /// <summary>High-level helper for M10 GPS-only configuration.</summary>
    public static class M10Configurator
    {
        public static async Task ConfigureGpsOnlyAsync(
            UbxClient client,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            await SendAndVerifyAsync(client, M10Commands.GpsEnable, cancellationToken).ConfigureAwait(false);
            await SendAndVerifyAsync(client, M10Commands.GalileoDisable, cancellationToken).ConfigureAwait(false);
            await SendAndVerifyAsync(client, M10Commands.BdsDisable, cancellationToken).ConfigureAwait(false);
            await SendAndVerifyAsync(client, M10Commands.GlonassDisable, cancellationToken).ConfigureAwait(false);
        }

        public static Task TriggerColdStartResetAsync(
            UbxClient client,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var cmd = M10Commands.CfgRstColdStart;
            return client.SendAsync(cmd.Class, cmd.MessageId, cmd.Payload, cancellationToken);
        }

        private static async Task SendAndVerifyAsync(UbxClient client, UbxCommand cmd, CancellationToken cancellationToken)
        {
            var ack = await client.SendConfigAsync(cmd.Class, cmd.MessageId, cmd.Payload, cancellationToken).ConfigureAwait(false);
            if (!ack)
            {
                throw new InvalidOperationException(
                    string.Format("NAK received for command class=0x{0:X2} id=0x{1:X2}.", (byte)cmd.Class, cmd.MessageId));
            }
        }
    }
}
