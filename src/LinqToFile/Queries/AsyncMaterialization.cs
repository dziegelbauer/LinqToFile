namespace LinqToFile.Queries;

internal static class AsyncMaterialization
{
    // Dictionary.TryAdd avoids the BCL duplicate exception, which includes the raw key.
    internal static async ValueTask<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(
        IAsyncEnumerable<T> rows, Func<T, TKey> keySelector, Func<T, TValue> valueSelector,
        IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken) where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>(comparer);
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.TryAdd(keySelector(row), valueSelector(row)))
                throw new ArgumentException("The query produced duplicate dictionary keys.");
        }
        return result;
    }
}
