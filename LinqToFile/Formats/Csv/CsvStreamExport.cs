using LinqToFile.Sources;
using LinqToFile.Writing;

namespace LinqToFile.Formats.Csv;

internal static class CsvStreamExport<T> where T : class, ITabularRecord<T>
{
    internal static long Write(StreamDestination destination, CsvOptions options, IEnumerable<T> rows)
    {
        StreamLease? lease = destination.Begin(false);
        bool complete = false;
        long count = 0;
        try
        {
            var output = new CsvOutput<T>(options);
            lease ??= destination.Open();
            var stream = lease.Value.Stream;
            RequireWritable(stream);
            var encoded = new EncodedOutput(stream, options.Encoding);
            var preamble = options.Encoding.GetPreamble();
            if (preamble.Length != 0) stream.Write(preamble);
            encoded.Write(output.Record, 0);
            foreach (var row in rows)
            {
                output.Row(row, checked(count + 1));
                encoded.Write(output.Record, count + 1);
                count++;
            }
            encoded.Write(ReadOnlyMemory<char>.Empty, count, true);
            stream.Flush();
            complete = true;
        }
        finally
        {
            if (lease is { } acquired)
            {
                if (complete) acquired.Dispose();
                else
                {
                    try { acquired.Dispose(); }
                    catch (Exception error) when (error is not OutOfMemoryException) { /* Preserve the export failure. */ }
                }
            }
        }
        return count;
    }

    internal static async ValueTask<long> WriteAsync(StreamDestination destination, CsvOptions options,
        IEnumerable<T>? rows, IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        StreamLease? lease = destination.Begin(true);
        bool complete = false;
        long count = 0;
        try
        {
            token.ThrowIfCancellationRequested();
            var output = new CsvOutput<T>(options);
            token.ThrowIfCancellationRequested();
            lease ??= await destination.OpenAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var stream = lease.Value.Stream;
            RequireWritable(stream);
            var encoded = new EncodedOutput(stream, options.Encoding);
            var preamble = options.Encoding.GetPreamble();
            if (preamble.Length != 0) await stream.WriteAsync(preamble, token).ConfigureAwait(false);
            await encoded.WriteAsync(output.Record, 0, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (asyncRows is not null)
            {
                await foreach (var row in asyncRows.WithCancellation(token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    output.Row(row, checked(count + 1));
                    await encoded.WriteAsync(output.Record, count + 1, token).ConfigureAwait(false);
                    count++;
                }
            }
            else
            {
                using var enumerator = rows!.GetEnumerator();
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (!enumerator.MoveNext()) break;
                    token.ThrowIfCancellationRequested();
                    output.Row(enumerator.Current, checked(count + 1));
                    await encoded.WriteAsync(output.Record, count + 1, token).ConfigureAwait(false);
                    count++;
                }
            }
            await encoded.WriteAsync(ReadOnlyMemory<char>.Empty, count, token, true).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            complete = true;
        }
        finally
        {
            if (lease is { } acquired)
            {
                if (complete) await acquired.DisposeAsync().ConfigureAwait(false);
                else
                {
                    try { await acquired.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { /* Preserve the export failure. */ }
                }
            }
        }
        token.ThrowIfCancellationRequested();
        return count;
    }

    private static void RequireWritable(Stream stream)
    {
        if (!stream.CanWrite) throw new InvalidOperationException("The destination stream must support writing.");
    }
}
