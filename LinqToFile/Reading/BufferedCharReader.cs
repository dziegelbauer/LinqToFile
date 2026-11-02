namespace LinqToFile.Reading;

internal sealed class BufferedCharReader(TextReader reader, int bufferSize)
{
    private readonly char[] buffer = new char[bufferSize];
    private readonly PositionedTextReader? positioned = reader as PositionedTextReader;
    internal long LastByteOffset { get; private set; }
    private int position;
    private int count;
    internal bool Ended { get; private set; }
    internal int Peek()
    {
        if (position == count && !Ended) Fill();
        return position < count ? buffer[position] : -1;
    }
    internal async ValueTask<int> PeekAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (position == count && !Ended) await FillAsync(token).ConfigureAwait(false);
        return position < count ? buffer[position] : -1;
    }

    internal bool TryRead(out char value)
    {
        if (position < count)
        {
            if (positioned is not null) LastByteOffset = positioned.ByteOffsets[position];
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
