namespace CSharpUbx;

public static class M10Configurator
{
    public static async Task ConfigureGpsOnlyAsync(
        Stream stream,
        bool useMeas50 = true,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(stream, M10Commands.GpsEnable, cancellationToken);
        await SendAsync(stream, M10Commands.GalileoDisable, cancellationToken);
        await SendAsync(stream, M10Commands.BdsDisable, cancellationToken);
        await SendAsync(stream, M10Commands.GlonassDisable, cancellationToken);
        await SendAsync(stream, useMeas50 ? M10Commands.RxmMeas50Enable : M10Commands.RxmMeas20Enable, cancellationToken);
    }

    public static Task TriggerColdStartResetAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return SendAsync(stream, M10Commands.CfgRstColdStart, cancellationToken);
    }

    public static async Task SendAsync(Stream stream, ReadOnlyMemory<byte> command, CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(stream));
        }

        await stream.WriteAsync(command, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
