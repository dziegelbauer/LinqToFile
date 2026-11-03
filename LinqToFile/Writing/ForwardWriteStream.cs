namespace LinqToFile.Writing;

// A fresh non-seekable view makes ZIP offsets relative to this export, without querying caller state.
internal sealed class ForwardWriteStream(Stream stream, CancellationToken token, bool asynchronous = false) : Stream
{
    private byte[] pending = [];
    private int pendingCount;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { token.ThrowIfCancellationRequested(); if (!asynchronous) stream.Flush(); }
    public override async Task FlushAsync(CancellationToken cancellationToken)
    { await DrainAsync().ConfigureAwait(false); token.ThrowIfCancellationRequested(); await stream.FlushAsync(token).ConfigureAwait(false); }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        token.ThrowIfCancellationRequested();
        if (!asynchronous) { stream.Write(buffer); return; }
        int required = checked(pendingCount + buffer.Length);
        if (required > pending.Length) Array.Resize(ref pending, Math.Max(required, Math.Max(8192, checked(pending.Length * 2))));
        buffer.CopyTo(pending.AsSpan(pendingCount)); pendingCount = required;
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await DrainAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }
    internal async ValueTask DrainAsync()
    {
        token.ThrowIfCancellationRequested();
        if (pendingCount == 0) return;
        await stream.WriteAsync(pending.AsMemory(0, pendingCount), token).ConfigureAwait(false);
        pendingCount = 0;
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    // Destination ownership belongs to the surrounding export, not this view.
}
