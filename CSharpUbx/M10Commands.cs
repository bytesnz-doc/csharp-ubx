namespace CSharpUbx;

public readonly record struct UbxCommand(UbxClass Class, byte MessageId, byte[] Payload);

public static class M10Commands
{
    // CFG-VALSET: enable GPS constellation
    public static readonly UbxCommand GpsEnable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("000100001f00311001"));

    // CFG-VALSET: disable Galileo constellation
    public static readonly UbxCommand GalileoDisable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("000100002100311000"));

    // CFG-VALSET: disable BDS constellation
    public static readonly UbxCommand BdsDisable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("000100002200311000"));

    // CFG-VALSET: disable GLONASS constellation
    public static readonly UbxCommand GlonassDisable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("000100002500311000"));

    // CFG-VALSET: enable RXM-MEAS50 output
    public static readonly UbxCommand RxmMeas50Enable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("00010000490691200 1"));

    // CFG-VALSET: enable RXM-MEAS20 output
    public static readonly UbxCommand RxmMeas20Enable =
        new(UbxClass.Cfg, (byte)CfgMessageId.Valset, FromHex("000100004406912001"));

    // CFG-RST: cold start
    public static readonly UbxCommand CfgRstColdStart =
        new(UbxClass.Cfg, (byte)CfgMessageId.Rst, FromHex("ffff0100"));

    private static byte[] FromHex(string hex) =>
        Convert.FromHexString(hex.Replace(" ", string.Empty));
}
