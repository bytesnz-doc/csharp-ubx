namespace CSharpUbx;

public enum AckMessageId : byte
{
    Nak = 0x00,
    Ack = 0x01,
}

public enum CfgMessageId : byte
{
    Rst    = 0x04,
    Valset = 0x8A,
}

public enum RxmMessageId : byte
{
    Meas20 = 0x84,
    Meas50 = 0x86,
}
