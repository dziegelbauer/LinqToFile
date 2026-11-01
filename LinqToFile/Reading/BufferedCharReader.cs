namespace LinqToFile.Reading;

internal sealed class BufferedCharReader(TextReader reader, int bufferSize)
{
    private readonly char[] buffer = new char[bufferSize];
    private int position;
    private int count;
    internal bool Ended { get; private set; }

    internal bool TryRead(out char value)
    {
        if (position < count)
        {
            value = buffer[position++];
            return true;
        }
        value = default;
        return false;
    }

    internal void Fill()
    {
        count = reader.Read(buffer.AsSpan());
        position = 0;
        Ended = count == 0;
    }

    internal async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        position = 0;
        Ended = count == 0;
    }
}
