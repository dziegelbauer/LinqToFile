namespace LinqToFile.Writing;

internal static class StreamSegments
{
    internal static void CopyExactly(Stream source, Stream destination, long length, byte[] buffer)
    {
        while (length > 0)
        {
            int count = (int)Math.Min(buffer.Length, length);
            source.ReadExactly(buffer.AsSpan(0, count));
            destination.Write(buffer.AsSpan(0, count));
            length -= count;
        }
    }
    internal static async ValueTask CopyExactlyAsync(Stream source, Stream destination, long length, byte[] buffer, CancellationToken token)
    {
        while (length > 0)
        {
            token.ThrowIfCancellationRequested();
            int count = (int)Math.Min(buffer.Length, length);
            await source.ReadExactlyAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            length -= count;
        }
    }

    // [suffix][new block] -> [new block][suffix]. No temporary storage or input replay.
    internal static void Rotate(Stream stream, long start, long originalEnd, byte[] left, byte[] right)
    {
        long added = stream.Length - originalEnd;
        if (start == originalEnd || added == 0) return;
        Reverse(stream, start, originalEnd - start, left, right);
        Reverse(stream, originalEnd, added, left, right);
        Reverse(stream, start, stream.Length - start, left, right);
    }
    private static void Reverse(Stream stream, long start, long length, byte[] left, byte[] right)
    {
        while (length > 1)
        {
            int count = (int)Math.Min(Math.Min(left.Length, right.Length), length / 2);
            long end = start + length - count;
            stream.Position = start; stream.ReadExactly(left.AsSpan(0, count));
            stream.Position = end; stream.ReadExactly(right.AsSpan(0, count));
            left.AsSpan(0, count).Reverse(); right.AsSpan(0, count).Reverse();
            stream.Position = start; stream.Write(right.AsSpan(0, count));
            stream.Position = end; stream.Write(left.AsSpan(0, count));
            start += count; length -= 2L * count;
        }
    }
    internal static async ValueTask RotateAsync(Stream stream, long start, long originalEnd, byte[] left, byte[] right)
    {
        if (start == originalEnd) return;
        // Flush asynchronously before seeking so a buffered FileStream cannot turn a
        // position change into a synchronous data write.
        await stream.FlushAsync().ConfigureAwait(false);
        long added = stream.Length - originalEnd;
        if (added == 0) return;
        await ReverseAsync(stream, start, originalEnd - start, left, right).ConfigureAwait(false);
        await ReverseAsync(stream, originalEnd, added, left, right).ConfigureAwait(false);
        await ReverseAsync(stream, start, stream.Length - start, left, right).ConfigureAwait(false);
    }
    private static async ValueTask ReverseAsync(Stream stream, long start, long length, byte[] left, byte[] right)
    {
        while (length > 1)
        {
            int count = (int)Math.Min(Math.Min(left.Length, right.Length), length / 2);
            long end = start + length - count;
            stream.Position = start; await stream.ReadExactlyAsync(left.AsMemory(0, count)).ConfigureAwait(false);
            stream.Position = end; await stream.ReadExactlyAsync(right.AsMemory(0, count)).ConfigureAwait(false);
            left.AsSpan(0, count).Reverse(); right.AsSpan(0, count).Reverse();
            stream.Position = start; await stream.WriteAsync(right.AsMemory(0, count)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            stream.Position = end; await stream.WriteAsync(left.AsMemory(0, count)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            start += count; length -= 2L * count;
        }
    }
}
