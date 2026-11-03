namespace LinqToFile.Sources;

// Identity is shared by all immutable configurations derived from one supplied destination.
internal sealed class StreamDestination
{
    private readonly Stream? stream;
    private readonly Func<Stream>? factory;
    private readonly Func<CancellationToken, ValueTask<Stream>>? asyncFactory;
    private readonly bool leaveOpen;
    private int claimed;

    internal StreamDestination(Stream stream, bool leaveOpen) { this.stream = stream; this.leaveOpen = leaveOpen; }
    internal StreamDestination(Func<Stream> factory) => this.factory = factory;
    internal StreamDestination(Func<CancellationToken, ValueTask<Stream>> factory) => asyncFactory = factory;

    internal StreamLease? Begin(bool asynchronous)
    {
        if (!asynchronous && asyncFactory is not null)
            throw new InvalidOperationException("This destination requires WriteAsync.");
        if (stream is null) return null;
        if (Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
            throw new InvalidOperationException("A supplied stream permits only one export across all derived configurations.");
        return new StreamLease(stream, !leaveOpen);
    }

    // Called only for factories, after model/header validation and cancellation checks.
    internal StreamLease Open() => new(RequireStream(factory!()), true);
    internal ValueTask<StreamLease> OpenAsync(CancellationToken token) => factory is not null
        ? new(new StreamLease(RequireStream(factory()), true)) : OpenFactoryAsync(token);

    private async ValueTask<StreamLease> OpenFactoryAsync(CancellationToken token) =>
        new(RequireStream(await asyncFactory!(token).ConfigureAwait(false)), true);

    private static Stream RequireStream(Stream? value) =>
        value ?? throw new InvalidOperationException("The stream factory returned null.");
}
