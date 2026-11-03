namespace LinqToFile.Writing;

internal sealed class WriteInput<T> : IAsyncDisposable
{
    private readonly IEnumerator<T>? synchronous;
    private readonly IAsyncEnumerator<T>? asynchronous;
    internal WriteInput(IEnumerable<T>? rows, IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        if (asyncRows is not null) asynchronous = asyncRows.GetAsyncEnumerator(token);
        else synchronous = rows!.GetEnumerator();
    }
    internal T Current => asynchronous is not null ? asynchronous.Current : synchronous!.Current;
    internal ValueTask<bool> MoveNextAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return asynchronous is not null ? asynchronous.MoveNextAsync() : new(synchronous!.MoveNext());
    }
    public ValueTask DisposeAsync()
    {
        if (asynchronous is not null) return asynchronous.DisposeAsync();
        synchronous!.Dispose();
        return default;
    }
}
