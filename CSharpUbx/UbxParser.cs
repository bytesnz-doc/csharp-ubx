namespace CSharpUbx;

public sealed class UbxParser
{
    private readonly List<byte> _buffer = [];

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
            var frameLength = checked(8 + payloadLength);

            if (cursor + frameLength > _buffer.Count)
            {
                break;
            }

            var classAndLengthAndPayload = _buffer
                .GetRange(cursor + 2, 4 + payloadLength)
                .ToArray();
            var checksum = UbxChecksum.Compute(classAndLengthAndPayload);
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
