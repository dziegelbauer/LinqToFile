namespace LinqToFile.Sources;

// Source identity is shared by all configurations derived from one supplied-stream source.
internal sealed class StreamSource
{
    private readonly string? path;
    private readonly Stream? stream;
    private readonly Func<Stream>? factory;
    private readonly Func<CancellationToken, ValueTask<Stream>>? asyncFactory;
    private readonly bool leaveOpen;
    private int claimed;

    internal StreamSource(string path) => this.path = path;
    internal StreamSource(Func<Stream> factory) => this.factory = factory;
    internal StreamSource(Func<CancellationToken, ValueTask<Stream>> asyncFactory) => this.asyncFactory = asyncFactory;
    internal StreamSource(Stream stream, bool leaveOpen) { this.stream = stream; this.leaveOpen = leaveOpen; }

    internal StreamLease Open()
    {
        if (stream is not null) return ClaimStream();
        if (path is not null)
            return new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan), true);
        if (factory is not null) return new(RequireStream(factory()), true);
        throw new InvalidOperationException("This source requires asynchronous enumeration. Use AsAsyncEnumerable() or an async materializer.");
    }

    internal ValueTask<StreamLease> OpenAsync(CancellationToken cancellationToken)
    {
        // Claim before checking cancellation. The query disposes the returned lease even for a cancelled first move.
        if (stream is not null) return new(ClaimStream());
        cancellationToken.ThrowIfCancellationRequested();
        if (path is not null)
            return new(new StreamLease(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan), true));
        if (factory is not null) return new(new StreamLease(RequireStream(factory()), true));
        return OpenFactoryAsync(cancellationToken);
    }

    private async ValueTask<StreamLease> OpenFactoryAsync(CancellationToken cancellationToken) =>
        new(RequireStream(await asyncFactory!(cancellationToken).ConfigureAwait(false)), true);

    private StreamLease ClaimStream()
    {
        if (Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
            throw new InvalidOperationException("A supplied stream permits only one enumeration across all derived configurations.");
        return new(stream!, !leaveOpen);
    }

    private static Stream RequireStream(Stream? value) =>
        value ?? throw new InvalidOperationException("The stream factory returned null.");
}
