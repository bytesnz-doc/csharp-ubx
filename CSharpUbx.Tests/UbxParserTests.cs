using System;
using System.Linq;
using CSharpUbx;
using Xunit;

namespace CSharpUbx.Tests
{
    public class UbxParserTests
    {
        [Fact]
        public void Feed_ShouldParseCompleteFrame()
        {
            var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var frame = UbxClient.BuildFrame((byte)UbxClass.Cfg, (byte)CfgMessageId.Valset, payload);
            var parser = new UbxParser();

            var messages = parser.Feed(frame);

            var message = Assert.Single(messages);
            Assert.Equal((byte)UbxClass.Cfg, message.MessageClass);
            Assert.Equal((byte)CfgMessageId.Valset, message.MessageId);
            Assert.Equal(payload, message.Payload);
        }

        [Fact]
        public void Feed_ShouldParseFrameArrivingInMultipleChunks()
        {
            var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            var frame = UbxClient.BuildFrame((byte)UbxClass.Cfg, (byte)CfgMessageId.Valset, payload);
            var parser = new UbxParser();

            var firstBatch = parser.Feed(frame, 0, 15);
            var secondBatch = parser.Feed(frame, 15, frame.Length - 15);

            Assert.Empty(firstBatch);
            var message = Assert.Single(secondBatch);
            Assert.Equal(payload, message.Payload);
        }

        [Fact]
        public void Feed_ShouldDiscardFrameWithBadChecksum()
        {
            var payload = new byte[] { 0xAA, 0xBB, 0xCC };
            var frame = UbxClient.BuildFrame((byte)UbxClass.Mon, 0x09, payload);
            frame[frame.Length - 1] ^= 0xFF;
            var parser = new UbxParser();

            var messages = parser.Feed(frame);

            Assert.Empty(messages);
        }

        [Fact]
        public void Feed_ShouldParseMultipleBackToBackFrames()
        {
            var payloadA = new byte[] { 0x01 };
            var payloadB = new byte[] { 0x02, 0x03 };
            var frames = UbxClient.BuildFrame((byte)UbxClass.Ack, (byte)AckMessageId.Ack, payloadA)
                .Concat(UbxClient.BuildFrame((byte)UbxClass.Ack, (byte)AckMessageId.Nak, payloadB))
                .ToArray();
            var parser = new UbxParser();

            var messages = parser.Feed(frames);

            Assert.Equal(2, messages.Count);
            Assert.Equal(payloadA, messages[0].Payload);
            Assert.Equal(payloadB, messages[1].Payload);
        }

        [Fact]
        public void BuildFrame_ShouldProduceValidChecksummedFrame()
        {
            var payload = new byte[] { 0x10, 0x20, 0x30 };
            var frame = UbxClient.BuildFrame((byte)UbxClass.Cfg, (byte)CfgMessageId.Valset, payload);

            Assert.Equal(UbxMessage.SyncChar1, frame[0]);
            Assert.Equal(UbxMessage.SyncChar2, frame[1]);
            Assert.Equal((byte)UbxClass.Cfg, frame[2]);
            Assert.Equal((byte)CfgMessageId.Valset, frame[3]);
            Assert.Equal(payload.Length, frame[4] | (frame[5] << 8));

            var parsed = Assert.Single(new UbxParser().Feed(frame));
            Assert.Equal(payload, parsed.Payload);
        }

        [Fact]
        public void BuildFrame_ShouldWorkWithEmptyPayload()
        {
            var frame = UbxClient.BuildFrame((byte)UbxClass.Ack, (byte)AckMessageId.Ack, new byte[0]);
            Assert.Equal(8, frame.Length);

            var parsed = Assert.Single(new UbxParser().Feed(frame));
            Assert.Empty(parsed.Payload);
        }
    }
}
