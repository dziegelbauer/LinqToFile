namespace LinqToFile.Formats.Xlsx;

// ZIP offsets are relative to the workbook origin, not the caller's absolute stream origin.
internal sealed class ArchiveViewStream : Stream
{
    private readonly Stream stream;
    private readonly long origin;
    private readonly long length;
    internal ArchiveViewStream(Stream stream)
    {
        if (!stream.CanSeek) throw new XlsxFormatException("Non-seekable input is not supported in XLSX stage 1.");
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
        long remaining = length - Position;
        return remaining <= 0 ? 0 : stream.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
    }
    public override long Seek(long offset, SeekOrigin seekOrigin)
    {
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
