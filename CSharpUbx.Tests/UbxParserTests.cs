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
        Assert.Equal(payload, meas50.Payload);
        Assert.True(message.IsRxmMeas50());
    }

    [Fact]
    public void BuildFrame_ShouldProduceValidChecksummedFrame()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var frame = UbxClient.BuildFrame((byte)UbxClass.Cfg, (byte)CfgMessageId.Valset, payload);

        Assert.Equal(UbxMessage.SyncChar1, frame[0]);
        Assert.Equal(UbxMessage.SyncChar2, frame[1]);
        Assert.Equal((byte)UbxClass.Cfg, frame[2]);
        Assert.Equal((byte)CfgMessageId.Valset, frame[3]);
        Assert.Equal(3, frame[4] | (frame[5] << 8));

        var messages = new UbxParser().Feed(frame);
        var parsed = Assert.Single(messages);
        Assert.Equal(payload, parsed.Payload.ToArray());
    }

    private static byte[] BuildFrame(byte messageClass, byte messageId, byte[] payload) =>
        UbxClient.BuildFrame(messageClass, messageId, payload);
}

