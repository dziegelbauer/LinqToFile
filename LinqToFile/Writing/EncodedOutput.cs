using System.Text;

namespace LinqToFile.Writing;

// A persistent encoder supports split surrogate pairs and stateful/custom encodings.
internal sealed class EncodedOutput
{
    private readonly Encoder encoder;
    private readonly byte[] buffer;
    private readonly Stream stream;
    internal EncodedOutput(Stream stream, Encoding encoding)
    {
        this.stream = stream;
        encoder = encoding.GetEncoder();
        buffer = new byte[Math.Max(8192, encoding.GetMaxByteCount(2))];
    }
    private int Convert(ReadOnlyMemory<char> text, int offset, bool flush, long row, out int consumed, out bool completed)
    {
        try
        {
            encoder.Convert(text.Span[offset..], buffer, flush, out consumed, out int count, out completed);
            return count;
        }
        catch (EncoderFallbackException)
        {
            if (row == 0) throw new TabularSchemaException("A generated header cannot be encoded.", 1, 1, 1);
            throw new TabularWriteException(row);
        }
    }
    internal void Write(ReadOnlyMemory<char> text, long row, bool flush = false)
    {
        int offset = 0;
        bool completed;
        do
        {
            int count = Convert(text, offset, flush, row, out int consumed, out completed);
            offset += consumed;
            stream.Write(buffer.AsSpan(0, count));
        } while (!completed);
    }
    internal async ValueTask WriteAsync(ReadOnlyMemory<char> text, long row, CancellationToken token, bool flush = false)
    {
        int offset = 0;
        bool completed;
        do
        {
            token.ThrowIfCancellationRequested();
            int count = Convert(text, offset, flush, row, out int consumed, out completed);
            offset += consumed;
            await stream.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
        } while (!completed);
    }
}
