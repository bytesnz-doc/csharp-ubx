namespace CSharpUbx;

public static class UbxChecksum
{
    public static (byte CkA, byte CkB) Compute(ReadOnlySpan<byte> data)
    {
        byte ckA = 0;
        byte ckB = 0;

        foreach (var value in data)
        {
            unchecked
            {
                ckA += value;
                ckB += ckA;
            }
        }

        return (ckA, ckB);
    }
}
