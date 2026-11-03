namespace LinqToFile.Tests.Support;

internal sealed class ExportProbeStream(bool asyncOnly = false, bool writable = true) : Stream
{
    private readonly MemoryStream storage = new();
    internal int SyncWrites { get; private set; }
    internal int AsyncWrites { get; private set; }
    internal int SyncFlushes { get; private set; }
    internal int AsyncFlushes { get; private set; }
    internal int SyncDisposals { get; private set; }
    internal int AsyncDisposals { get; private set; }
    internal bool IsDisposed { get; private set; }
    internal Action? AfterWrite { get; init; }
    internal Func<CancellationToken, ValueTask>? BeforeAsyncWrite { get; init; }
    internal Func<CancellationToken, Task>? BeforeAsyncFlush { get; init; }
    internal Func<ValueTask>? BeforeAsyncDispose { get; init; }
    internal Exception? WriteFailure { get; init; }
    internal Exception? FlushFailure { get; init; }
    internal Exception? DisposeFailure { get; init; }
    internal byte[] Bytes => storage.ToArray();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => writable && !IsDisposed;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (asyncOnly) throw new InvalidOperationException("Synchronous output is forbidden.");
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        SyncWrites++;
        if (WriteFailure is not null) throw WriteFailure;
        storage.Write(buffer);
        AfterWrite?.Invoke();
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        AsyncWrites++;
        if (BeforeAsyncWrite is not null) await BeforeAsyncWrite(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (WriteFailure is not null) throw WriteFailure;
        storage.Write(buffer.Span);
        AfterWrite?.Invoke();
    }
    public override void Flush()
    {
        if (asyncOnly) throw new InvalidOperationException("Synchronous flushing is forbidden.");
        SyncFlushes++;
        if (FlushFailure is not null) throw FlushFailure;
    }
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AsyncFlushes++;
        if (BeforeAsyncFlush is not null) await BeforeAsyncFlush(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (FlushFailure is not null) throw FlushFailure;
    }
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        SyncDisposals++; IsDisposed = true;
        storage.Dispose();
        if (DisposeFailure is not null) throw DisposeFailure;
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;
        AsyncDisposals++;
        if (BeforeAsyncDispose is not null) await BeforeAsyncDispose();
        IsDisposed = true; storage.Dispose();
        GC.SuppressFinalize(this);
        if (DisposeFailure is not null) throw DisposeFailure;
    }
}
