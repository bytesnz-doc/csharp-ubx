namespace CSharpUbx;

public enum UbxClass : byte
{
    Nav = 0x01,
    Rxm = 0x02,
    Inf = 0x04,
    Ack = 0x05,
    Cfg = 0x06,
    Mon = 0x0A,
    Tim = 0x0D,
    Sec = 0x27,
}
