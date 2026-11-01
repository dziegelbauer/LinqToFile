namespace LinqToFile;

internal sealed class BufferedCharReader(TextReader reader, int bufferSize)
{
    private readonly char[] buffer = new char[bufferSize];
    private int position;
    private int count;
    private bool ended;

    internal int Read()
    {
        if (position == count && !Fill()) return -1;
        return buffer[position++];
    }

    internal int Peek()
    {
        if (position == count && !Fill()) return -1;
        return buffer[position];
    }

    private bool Fill()
    {
        if (ended) return false;
        count = reader.Read(buffer.AsSpan());
        position = 0;
        ended = count == 0;
        return !ended;
    }
}
