namespace LinqToFile.Formats.Xlsx;

// ZIP offsets are relative to the workbook origin, not the caller's absolute stream origin.
internal sealed class ArchiveViewStream : Stream
{
    private readonly Stream stream;
    private readonly long origin;
    private readonly long length;
    private readonly CancellationToken cancellation;
    internal ArchiveViewStream(Stream stream, CancellationToken cancellation = default)
    {
        this.cancellation = cancellation;
        if (!stream.CanSeek) throw new XlsxFormatException("The archive requires seekable input.");
        this.stream = stream;
        origin = stream.Position;
        length = stream.Length - origin;
        if (origin < 0 || length < 0) throw new XlsxFormatException("The workbook stream position is invalid.");
    }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => stream.Position - origin; set => Seek(value, SeekOrigin.Begin); }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        cancellation.ThrowIfCancellationRequested();
        long remaining = length - Position;
        return remaining <= 0 ? 0 : stream.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long remaining = length - Position;
        return remaining <= 0 ? new(0) : stream.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken);
    }
    public override long Seek(long offset, SeekOrigin seekOrigin)
    {
        cancellation.ThrowIfCancellationRequested();
        long position = seekOrigin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(Position + offset),
            SeekOrigin.End => checked(length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(seekOrigin))
        };
        if (position < 0 || position > length) throw new XlsxFormatException("An archive seek is outside the workbook.");
        stream.Position = checked(origin + position);
        return position;
    }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    // The shared source lease owns the underlying stream.
}
