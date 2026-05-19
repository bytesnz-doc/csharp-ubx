namespace CSharpUbx;

public static class UbxMessageExtensions
{
    public static bool IsRxmMeas50(this UbxMessage message) =>
        message.MessageClass == RxmMeas50Message.ClassId &&
        message.MessageId == RxmMeas50Message.MessageId &&
        message.Payload.Length == RxmMeas50Message.PayloadLength;
}
