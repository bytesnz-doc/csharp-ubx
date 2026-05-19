namespace CSharpUbx.Tests;

public class M10ConfiguratorTests
{
    [Fact]
    public async Task ConfigureGpsOnlyAsync_ShouldSendExpectedSequenceWithMeas50()
    {
        await using var stream = new MemoryStream();

        await M10Configurator.ConfigureGpsOnlyAsync(stream, useMeas50: true);

        var expected = Concatenate(
            M10Commands.GpsEnable,
            M10Commands.GalileoDisable,
            M10Commands.BdsDisable,
            M10Commands.GlonassDisable,
            M10Commands.RxmMeas50Enable);

        Assert.Equal(expected, stream.ToArray());
    }

    [Fact]
    public async Task TriggerColdStartResetAsync_ShouldSendCfgRst()
    {
        await using var stream = new MemoryStream();

        await M10Configurator.TriggerColdStartResetAsync(stream);

        Assert.Equal(M10Commands.CfgRstColdStart, stream.ToArray());
    }

    private static byte[] Concatenate(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(a => a.Length)];
        var offset = 0;
        foreach (var arr in arrays)
        {
            Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
            offset += arr.Length;
        }

        return result;
    }
}
