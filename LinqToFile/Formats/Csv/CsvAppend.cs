using LinqToFile.Writing;

namespace LinqToFile.Formats.Csv;

internal static class CsvAppend<T> where T : class, ITabularRecord<T>
{
    private static FileStream Open(string path, CsvOptions options, bool asynchronous) =>
        new(path, FileMode.Open, options.InPlaceWrites ? FileAccess.ReadWrite : FileAccess.Read,
            options.InPlaceWrites ? FileShare.None : FileShare.Read, 4096,
            FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));

    private static string TemporaryPath(string destination) =>
        Path.Combine(Path.GetDirectoryName(destination)!, ".linqtofile-" + Guid.NewGuid().ToString("N") + ".tmp");

    internal static long Append(string path, CsvOptions options, IEnumerable<T> rows)
    {
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        string? temporary = null;
        bool owned = false;
        try
        {
            long count;
            using (var existing = Open(destination, options, false))
            {
                var prepared = CsvWritePreparation<T>.Read(existing, options);
                bool separator = prepared.NeedsSeparator(CsvWritePreparation<T>.Tail(existing));
                using var input = rows.GetEnumerator();
                if (!input.MoveNext()) return 0;
                prepared.Output.Row(input.Current, 1);
                temporary = options.InPlaceWrites ? null : TemporaryPath(destination);
                using var staged = temporary is null ? null : StagedFile.Open(temporary, existing, false);
                owned = staged is not null;
                if (staged is not null) { existing.Position = 0; existing.CopyTo(staged); }
                else existing.Position = existing.Length;
                Stream output = staged ?? existing;
                var encoded = new EncodedOutput(output, prepared.Encoding);
                if (separator) encoded.Write(prepared.Separator.AsMemory(), 1);
                encoded.Write(prepared.Output.Record, 1);
                count = 1;
                while (input.MoveNext())
                {
                    prepared.Output.Row(input.Current, checked(count + 1));
                    encoded.Write(prepared.Output.Record, count + 1);
                    count++;
                }
                encoded.Write(ReadOnlyMemory<char>.Empty, count, true);
            }
            // Source, target, and input enumeration have all closed before committing.
            if (temporary is not null) File.Replace(temporary, destination, null);
            return count;
        }
        finally { if (owned && temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static async ValueTask<long> AppendAsync(string path, CsvOptions options, IEnumerable<T>? rows,
        IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        using var cancellation = new WriteCancellation(token, options.InPlaceWrites);
        string? temporary = null;
        bool owned = false;
        try
        {
            long count = 0;
            await using (var existing = Open(destination, options, true))
            {
                var prepared = await CsvWritePreparation<T>.ReadAsync(existing, options, cancellation.Token).ConfigureAwait(false);
                bool separator = prepared.NeedsSeparator(await CsvWritePreparation<T>.TailAsync(existing, cancellation.Token).ConfigureAwait(false));
                await using var input = new WriteInput<T>(rows, asyncRows, cancellation.Token);
                bool hasRows = await input.MoveNextAsync(cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                if (hasRows)
                {
                    prepared.Output.Row(input.Current, 1);
                    temporary = options.InPlaceWrites ? null : TemporaryPath(destination);
                    await using var staged = temporary is null ? null : StagedFile.Open(temporary, existing, true);
                    owned = staged is not null;
                    if (staged is not null)
                    {
                        existing.Position = 0;
                        await existing.CopyToAsync(staged, cancellation.Token).ConfigureAwait(false);
                    }
                    else existing.Position = existing.Length;
                    Stream output = staged ?? existing;
                    var encoded = new EncodedOutput(output, prepared.Encoding);
                    if (options.InPlaceWrites) cancellation.BeginMutation();
                    if (separator) await encoded.WriteAsync(prepared.Separator.AsMemory(), 1, cancellation.Token).ConfigureAwait(false);
                    await encoded.WriteAsync(prepared.Output.Record, 1, cancellation.Token).ConfigureAwait(false);
                    count = 1;
                    while (await input.MoveNextAsync(cancellation.Token).ConfigureAwait(false))
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        prepared.Output.Row(input.Current, checked(count + 1));
                        await encoded.WriteAsync(prepared.Output.Record, count + 1, cancellation.Token).ConfigureAwait(false);
                        count++;
                    }
                    await encoded.WriteAsync(ReadOnlyMemory<char>.Empty, count, cancellation.Token, true).ConfigureAwait(false);
                    await output.FlushAsync(cancellation.Token).ConfigureAwait(false);
                }
            }
            if (count == 0) { token.ThrowIfCancellationRequested(); return 0; }
            if (temporary is not null)
            {
                token.ThrowIfCancellationRequested();
                File.Replace(temporary, destination, null);
            }
            return count;
        }
        finally { if (owned && temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }
}
