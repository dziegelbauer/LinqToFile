namespace LinqToFile.Formats.Xlsx;

// Per-enumeration accounting. Reopening a part counts its decompressed bytes again.
internal sealed class XlsxResources(XlsxOptions options, CancellationToken cancellationToken = default) : IDisposable, IAsyncDisposable
{
    private readonly CancellationToken cancellation = cancellationToken;
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
            Options = FileOptions.DeleteOnClose | FileOptions.RandomAccess | FileOptions.Asynchronous, BufferSize = 4096
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

    public async ValueTask DisposeAsync()
    {
        Exception? failure = null;
        foreach (var file in files)
            try { await file.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { failure ??= error; }
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
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            owner.cancellation.ThrowIfCancellationRequested();
            token.ThrowIfCancellationRequested();
            if (owner.DecompressedLimit is long maximum)
            {
                long remaining = maximum - owner.decompressed;
                if (remaining < buffer.Length) buffer = buffer[..(int)Math.Min(buffer.Length, remaining + 1)];
            }
            // XmlReader has no cancellation-token overload. The enumeration token reaches the underlying I/O here.
            int count = await stream.ReadAsync(buffer, owner.cancellation.CanBeCanceled ? owner.cancellation : token).ConfigureAwait(false);
            owner.cancellation.ThrowIfCancellationRequested();
            if (owner.decompressed > long.MaxValue - count || owner.DecompressedLimit is long limit && count > limit - owner.decompressed)
                throw XlsxPackage.Error("The decompressed-byte limit was exceeded.", part);
            owner.decompressed += count;
            return count;
        }
        public override ValueTask DisposeAsync() => stream.DisposeAsync();
        protected override void Dispose(bool disposing) { if (disposing) stream.Dispose(); base.Dispose(disposing); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
