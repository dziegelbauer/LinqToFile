namespace LinqToFile.Tests.Support;

internal sealed class ProbeStream(byte[] data, int chunkSize = int.MaxValue, bool asyncOnly = false, bool yieldReads = false, int initialOffset = 0) : Stream
{
    private int offset = initialOffset;
    internal int SyncReads { get; private set; }
    internal int AsyncReads { get; private set; }
    internal int SyncDisposals { get; private set; }
    internal int AsyncDisposals { get; private set; }
    internal bool IsDisposed { get; private set; }
    internal Func<CancellationToken, ValueTask>? BeforeReadAsync { get; init; }

    public override bool CanRead => !IsDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int start, int count) => Read(buffer.AsSpan(start, count));
    public override int Read(Span<byte> buffer)
    {
        if (asyncOnly) throw new InvalidOperationException("Synchronous I/O is forbidden for this stream.");
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        SyncReads++;
        return Copy(buffer);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        AsyncReads++;
        if (BeforeReadAsync is not null) await BeforeReadAsync(cancellationToken);
        if (yieldReads) await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return Copy(buffer.Span);
    }

    public override Task<int> ReadAsync(byte[] buffer, int start, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(start, count), cancellationToken).AsTask();

    private int Copy(Span<byte> buffer)
    {
        int length = Math.Min(Math.Min(buffer.Length, chunkSize), data.Length - offset);
        data.AsSpan(offset, length).CopyTo(buffer);
        offset += length;
        return length;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed && disposing) { SyncDisposals++; IsDisposed = true; }
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        if (!IsDisposed) { AsyncDisposals++; IsDisposed = true; }
        GC.SuppressFinalize(this);
        return default;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
