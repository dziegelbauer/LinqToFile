namespace LinqToFile.Formats.Xlsx;

// Per-enumeration accounting. Reopening a part counts its decompressed bytes again.
internal sealed class XlsxResources(XlsxOptions options) : IDisposable
{
    private long decompressed;
    private long temporary;
    private readonly List<FileStream> files = [];
    internal long DecompressedBytes => decompressed;
    internal long TemporaryBytes => temporary;
    private long? DecompressedLimit => options.MaxDecompressedBytes;

    internal Stream Count(Stream stream, string part) => new CountedStream(stream, this, part);
    internal void ReserveTemporary(long bytes)
    {
        if (bytes < 0 || temporary > long.MaxValue - bytes || options.MaxTemporaryBytes is long maximum && bytes > maximum - temporary)
            throw new XlsxFormatException("The temporary-storage limit was exceeded.");
        temporary += bytes;
    }

    internal FileStream CreateTemporary()
    {
        if (!options.TemporaryFiles) throw new XlsxFormatException("Temporary files are disabled but disk storage is required.");
        string directory = options.TemporaryDirectory ?? Path.GetTempPath();
        string path = Path.Combine(directory, "LinqToFile-" + Guid.NewGuid().ToString("N") + ".tmp");
        var settings = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
            Options = FileOptions.DeleteOnClose | FileOptions.RandomAccess, BufferSize = 4096
        };
        if (!OperatingSystem.IsWindows()) settings.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var file = new FileStream(path, settings);
        files.Add(file);
        return file;
    }

    public void Dispose()
    {
        // Dispose every handle even if one close fails. Each stream deletes its generated file.
        Exception? failure = null;
        foreach (var file in files)
            try { file.Dispose(); } catch (Exception error) { failure ??= error; }
        files.Clear();
        temporary = 0;
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class CountedStream(Stream stream, XlsxResources owner, string part) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (owner.DecompressedLimit is long maximum)
            {
                long remaining = maximum - owner.decompressed;
                if (remaining < buffer.Length) buffer = buffer[..(int)Math.Min(buffer.Length, remaining + 1)];
            }
            int count = stream.Read(buffer);
            if (owner.decompressed > long.MaxValue - count || owner.DecompressedLimit is long limit && count > limit - owner.decompressed)
                throw XlsxPackage.Error("The decompressed-byte limit was exceeded.", part);
            owner.decompressed += count;
            return count;
        }
        protected override void Dispose(bool disposing) { if (disposing) stream.Dispose(); base.Dispose(disposing); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
