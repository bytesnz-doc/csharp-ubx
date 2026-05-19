namespace CSharpUbx.Tests;

public class UbxParserTests
{
    [Fact]
    public void Feed_ShouldParseSplitRxmMeas50Frame()
    {
        var payload = Enumerable.Range(0, RxmMeas50Message.PayloadLength).Select(i => (byte)i).ToArray();
        var frame = BuildFrame(RxmMeas50Message.ClassId, RxmMeas50Message.MessageId, payload);
        var parser = new UbxParser();

        var firstBatch = parser.Feed(frame.AsSpan(0, 20));
        var secondBatch = parser.Feed(frame.AsSpan(20));

        Assert.Empty(firstBatch);
        var message = Assert.Single(secondBatch);
        Assert.Equal(RxmMeas50Message.ClassId, message.MessageClass);
        Assert.Equal(RxmMeas50Message.MessageId, message.MessageId);
        Assert.Equal(payload, message.Payload.ToArray());
    }

    [Fact]
    public void Feed_ShouldIgnoreBadChecksumFrame()
    {
        var payload = Enumerable.Repeat((byte)0x5A, RxmMeas50Message.PayloadLength).ToArray();
        var frame = BuildFrame(RxmMeas50Message.ClassId, RxmMeas50Message.MessageId, payload);
        frame[^1] ^= 0xFF;
        var parser = new UbxParser();

        var messages = parser.Feed(frame);

        Assert.Empty(messages);
    }

    [Fact]
    public void TryParse_ShouldAcceptRxmMeas50Payload()
    {
        var payload = Enumerable.Range(0, RxmMeas50Message.PayloadLength).Select(i => (byte)(255 - i)).ToArray();
        var message = new UbxMessage(RxmMeas50Message.ClassId, RxmMeas50Message.MessageId, payload);

        var parsed = RxmMeas50Message.TryParse(message, out var meas50);

        Assert.True(parsed);
        Assert.NotNull(meas50);
        Assert.Equal(payload, meas50.Payload);
        Assert.True(message.IsRxmMeas50());
    }

    private static byte[] BuildFrame(byte messageClass, byte messageId, byte[] payload)
    {
        var length = (ushort)payload.Length;
        var classIdLenPayload = new byte[4 + payload.Length];
        classIdLenPayload[0] = messageClass;
        classIdLenPayload[1] = messageId;
        classIdLenPayload[2] = (byte)(length & 0xFF);
        classIdLenPayload[3] = (byte)(length >> 8);
        Array.Copy(payload, 0, classIdLenPayload, 4, payload.Length);

        var checksum = UbxChecksum.Compute(classIdLenPayload);

        var frame = new byte[8 + payload.Length];
        frame[0] = UbxMessage.SyncChar1;
        frame[1] = UbxMessage.SyncChar2;
        Array.Copy(classIdLenPayload, 0, frame, 2, classIdLenPayload.Length);
        frame[^2] = checksum.CkA;
        frame[^1] = checksum.CkB;
        return frame;
    }
}
