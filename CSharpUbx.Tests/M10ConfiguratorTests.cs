using System;
using System.IO;
using System.Linq;
using System.Threading;
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

            var ackData = commands
                .SelectMany(cmd => UbxClient.BuildFrame(
                    (byte)UbxClass.Ack,
                    (byte)AckMessageId.Ack,
                    new byte[] { (byte)cmd.Class, cmd.MessageId }))
                .ToArray();

            using (var stream = new DuplexMemoryStream(ackData))
            {
                var client = new UbxClient(stream);

                await M10Configurator.ConfigureGpsOnlyAsync(client);

                var expectedWrites = commands
                    .SelectMany(cmd => UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload))
                    .ToArray();

                Assert.Equal(expectedWrites, stream.Written);
            }
        }

        [Fact]
        public async Task TriggerColdStartResetAsync_ShouldSendCfgRstFrame()
        {
            using (var stream = new DuplexMemoryStream(new byte[0]))
            {
                var client = new UbxClient(stream);

                await M10Configurator.TriggerColdStartResetAsync(client);

                var cmd = M10Commands.CfgRstColdStart;
                var expected = UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload);
                Assert.Equal(expected, stream.Written);
            }
        }

        private sealed class DuplexMemoryStream : Stream
        {
            private readonly MemoryStream _reads;
            private readonly MemoryStream _writes;

            public DuplexMemoryStream(byte[] readData)
            {
                _reads = new MemoryStream(readData);
                _writes = new MemoryStream();
            }

            public byte[] Written
            {
                get { return _writes.ToArray(); }
            }

            public override bool CanRead { get { return true; } }

            public override bool CanWrite { get { return true; } }

            public override bool CanSeek { get { return false; } }

            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _reads.Read(buffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _reads.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _writes.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _writes.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override void Flush()
            {
                _writes.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
        }
    }
}
