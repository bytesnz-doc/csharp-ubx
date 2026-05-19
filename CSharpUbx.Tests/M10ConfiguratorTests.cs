namespace CSharpUbx.Tests;

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

        // Pre-build the ACK responses the device would send back for each CFG-VALSET.
        var ackData = commands
            .SelectMany(cmd => UbxClient.BuildFrame(
                (byte)UbxClass.Ack, (byte)AckMessageId.Ack,
                [(byte)cmd.Class, cmd.MessageId]))
            .ToArray();

        using var stream = new DuplexMemoryStream(ackData);
        var client = new UbxClient(stream);

        await M10Configurator.ConfigureGpsOnlyAsync(client);

        // Verify all four config frames were written in order.
        var expectedWrites = commands
            .SelectMany(cmd => UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload))
            .ToArray();

        Assert.Equal(expectedWrites, stream.Written);
    }

    [Fact]
    public async Task TriggerColdStartResetAsync_ShouldSendCfgRstFrame()
    {
        using var stream = new DuplexMemoryStream([]);
        var client = new UbxClient(stream);

        await M10Configurator.TriggerColdStartResetAsync(client);

        var cmd = M10Commands.CfgRstColdStart;
        var expected = UbxClient.BuildFrame((byte)cmd.Class, cmd.MessageId, cmd.Payload);
        Assert.Equal(expected, stream.Written);
    }

    /// <summary>
    /// Minimal duplex stream for testing: pre-loaded read data, recorded writes.
    /// </summary>
    private sealed class DuplexMemoryStream(byte[] readData) : Stream
    {
        private readonly MemoryStream _reads = new(readData);
        private readonly MemoryStream _writes = new();

        public byte[] Written => _writes.ToArray();

        public override bool CanRead  => true;
        public override bool CanWrite => true;
        public override bool CanSeek  => false;
        public override long Length   => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)         => _reads.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _reads.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => _reads.ReadAsync(buffer, ct);

        public override void Write(byte[] buffer, int offset, int count)        => _writes.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            _writes.Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            _writes.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void  Flush()                                           => _writes.Flush();
        public override Task  FlushAsync(CancellationToken ct)                  => Task.CompletedTask;
        public override long  Seek(long offset, SeekOrigin origin)              => throw new NotSupportedException();
        public override void  SetLength(long value)                             => throw new NotSupportedException();
    }
}

