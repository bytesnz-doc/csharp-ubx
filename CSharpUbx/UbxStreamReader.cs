namespace CSharpUbx;

public sealed class UbxStreamReader
{
    private readonly Stream _stream;
    private readonly UbxParser _parser = new();

    public UbxStreamReader(Stream stream)
    {
        _stream = stream;
    }

    public async IAsyncEnumerable<UbxMessage> ReadMessagesAsync(
        int bufferSize = 1024,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }

        var readBuffer = new byte[bufferSize];

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, bufferSize), cancellationToken);
            if (bytesRead <= 0)
            {
                yield break;
            }

            var messages = _parser.Feed(readBuffer.AsSpan(0, bytesRead));
            foreach (var message in messages)
            {
                yield return message;
            }
        }
    }
}
