using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpUbx;
using Xunit;

namespace CSharpUbx.Tests
{
    public class M10ConfiguratorTests
    {
        [Fact]
        public async Task ConfigureGpsOnlyAsync_ShouldSendFourConfigFramesAndReceiveAcks()
        {
            var commands = new[]
            {
                M10Commands.GpsEnable,
                M10Commands.GalileoDisable,
                M10Commands.BdsDisable,
                M10Commands.GlonassDisable,
            };

            var written = new List<byte>();
            var clientHolder = new UbxClient[1];

            clientHolder[0] = new UbxClient((buf, off, count) =>
            {
                for (var i = 0; i < count; i++)
                {
                    written.Add(buf[off + i]);
                }

                // Immediately feed back the matching ACK so SendConfigAsync can unblock.
                var cls = buf[off + 2];
                var id = buf[off + 3];
                var ack = UbxClient.BuildFrame((byte)UbxClass.Ack, (byte)AckMessageId.Ack, new byte[] { cls, id });
                clientHolder[0].FeedBytes(ack, 0, ack.Length);
            });

            await M10Configurator.ConfigureGpsOnlyAsync(clientHolder[0]);

            var expectedWrites = commands
                .SelectMany(cmd => UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload))
                .ToArray();

            Assert.Equal(expectedWrites, written.ToArray());
        }

        [Fact]
        public async Task TriggerColdStartResetAsync_ShouldSendCfgRstFrame()
        {
            var written = new List<byte>();
            var client = new UbxClient((buf, off, count) =>
            {
                for (var i = 0; i < count; i++)
                {
                    written.Add(buf[off + i]);
                }
            });

            await M10Configurator.TriggerColdStartResetAsync(client);

            var cmd = M10Commands.CfgRstColdStart;
            var expected = UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload);
            Assert.Equal(expected, written.ToArray());
        }
    }
}
