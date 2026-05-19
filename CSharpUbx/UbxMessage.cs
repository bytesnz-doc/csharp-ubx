namespace CSharpUbx;

public readonly record struct UbxMessage(byte MessageClass, byte MessageId, byte[] Payload)
{
    public const byte SyncChar1 = 0xB5;
    public const byte SyncChar2 = 0x62;
}
