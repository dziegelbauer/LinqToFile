namespace LinqToFile.Sources;

internal readonly struct StreamLease(Stream stream, bool ownsStream) : IDisposable, IAsyncDisposable
{
    internal Stream Stream { get; } = stream;

    public void Dispose()
    {
        if (ownsStream) Stream.Dispose();
    }

    public ValueTask DisposeAsync() => ownsStream ? Stream.DisposeAsync() : default;
}
