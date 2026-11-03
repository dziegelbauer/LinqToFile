namespace LinqToFile.Formats.Xlsx;

// A forward-only temporary-file writer. Resource lifetime follows the operation, including closed staging files.
internal sealed class XlsxMutationOutputStream(Stream stream, XlsxResources resources, long? maximumBytes = null) : Stream
{
    private long written;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    private void Reserve(int count)
    {
        if (count < 0 || written > long.MaxValue - count || maximumBytes is { } maximum && count > maximum - written)
            throw new XlsxFormatException("The replacement workbook-byte limit was exceeded.");
        resources.ReserveTemporary(count);
        written += count;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Reserve(buffer.Length);
        stream.Write(buffer);
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
        WriteAsync(buffer.AsMemory(offset, count), token).AsTask();
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reserve(buffer.Length);
        return stream.WriteAsync(buffer, cancellationToken);
    }
    public override void Flush() => stream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => stream.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    // The surrounding operation owns the file and its disposal/publication. This view never closes it.
}
