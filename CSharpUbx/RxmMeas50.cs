namespace CSharpUbx;

public sealed class RxmMeas50Message
{
    public const byte ClassId = 0x02;
    public const byte MessageId = 0x86;
    public const int PayloadLength = 50;

    public required byte[] Payload { get; init; }

    public static bool TryParse(UbxMessage message, out RxmMeas50Message? result)
    {
        result = null;

        if (message.MessageClass != ClassId || message.MessageId != MessageId)
        {
            return false;
        }

        if (message.Payload.Length != PayloadLength)
        {
            return false;
        }

        result = new RxmMeas50Message
        {
            Payload = message.Payload.ToArray()
        };

        return true;
    }
}
