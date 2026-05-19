namespace CSharpUbx;

public static class M10Commands
{
    public static readonly byte[] GpsEnable = FromHex("b562068a0900000100001f00311001fb80");
    public static readonly byte[] GalileoDisable = FromHex("b562068a0900000100002100311000fc89");
    public static readonly byte[] BdsDisable = FromHex("b562068a0900000100002200311000fd8e");
    public static readonly byte[] GlonassDisable = FromHex("b562068a0900000100002500311000009d");
    public static readonly byte[] RxmMeas50Enable = FromHex("b562068a09000001000049069120019baa");
    public static readonly byte[] RxmMeas20Enable = FromHex("b562068a09000001000044069120019691");
    public static readonly byte[] CfgRstColdStart = FromHex("b56206040400ffff01000d5f");

    private static byte[] FromHex(string hex)
    {
        return Convert.FromHexString(hex);
    }
}
