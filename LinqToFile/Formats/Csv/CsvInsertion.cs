using LinqToFile.Writing;

namespace LinqToFile.Formats.Csv;

internal static class CsvInsertion<T> where T : class, ITabularRecord<T>
{
    private const int BlockSize = 64 * 1024;
    private static string TemporaryPath(string destination) =>
        Path.Combine(Path.GetDirectoryName(destination)!, ".linqtofile-" + Guid.NewGuid().ToString("N") + ".tmp");
    private static void CheckBoundary(long offset)
    {
        if (offset < 0) throw new NotSupportedException("The selected record begins inside a decoder fallback and has no independent byte boundary.");
    }

    internal static long Insert(string path, CsvOptions options, long position, IEnumerable<T> rows, IWriteFileSystem? files = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        files ??= PhysicalWriteFileSystem.Instance;
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        string? temporary = null;
        bool owned = false;
        try
        {
            long count;
            using (var existing = files.OpenExisting(destination, options.InPlaceWrites, false))
            {
                var plan = CsvInsertionPreparation<T>.Read(existing, options, position);
                var prepared = plan.Formatting;
                using var input = rows.GetEnumerator();
                if (!input.MoveNext()) return 0;
                CheckBoundary(plan.Offset);
                prepared.Output.Row(input.Current, 1);
                var left = new byte[BlockSize];
                var right = options.InPlaceWrites ? new byte[BlockSize] : null;
                bool separator = plan.Offset == plan.OriginalEnd &&
                    prepared.NeedsSeparator(CsvWritePreparation<T>.Tail(existing));
                temporary = options.InPlaceWrites ? null : TemporaryPath(destination);
                using var staged = temporary is null ? null : files.OpenStaged(temporary, existing, false);
                owned = staged is not null;
                if (staged is not null)
                {
                    existing.Position = 0;
                    StreamSegments.CopyExactly(existing, staged, plan.Offset, left);
                }
                else existing.Position = plan.OriginalEnd;
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
                if (staged is not null)
                    StreamSegments.CopyExactly(existing, staged, plan.OriginalEnd - plan.Offset, left);
                else
                    StreamSegments.Rotate(existing, plan.Offset, plan.OriginalEnd, left, right!);
            }
            if (temporary is not null) files.Replace(temporary, destination);
            return count;
        }
        finally { if (owned && temporary is not null) files.DeleteTemporary(temporary); }
    }

    internal static async ValueTask<long> InsertAsync(string path, CsvOptions options, long position, IEnumerable<T>? rows,
        IAsyncEnumerable<T>? asyncRows, CancellationToken token, IWriteFileSystem? files = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        token.ThrowIfCancellationRequested();
        files ??= PhysicalWriteFileSystem.Instance;
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        using var cancellation = new WriteCancellation(token, options.InPlaceWrites);
        string? temporary = null;
        bool owned = false;
        try
        {
            long count = 0;
            await using (var existing = files.OpenExisting(destination, options.InPlaceWrites, true))
            {
                var plan = await CsvInsertionPreparation<T>.ReadAsync(existing, options, position, cancellation.Token).ConfigureAwait(false);
                var prepared = plan.Formatting;
                await using var input = new WriteInput<T>(rows, asyncRows, cancellation.Token);
                bool hasRows = await input.MoveNextAsync(cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                if (hasRows)
                {
                    CheckBoundary(plan.Offset);
                    prepared.Output.Row(input.Current, 1);
                    var left = new byte[BlockSize];
                    var right = options.InPlaceWrites ? new byte[BlockSize] : null;
                    bool separator = plan.Offset == plan.OriginalEnd && prepared.NeedsSeparator(
                        await CsvWritePreparation<T>.TailAsync(existing, cancellation.Token).ConfigureAwait(false));
                    temporary = options.InPlaceWrites ? null : TemporaryPath(destination);
                    await using var staged = temporary is null ? null : files.OpenStaged(temporary, existing, true);
                    owned = staged is not null;
                    if (staged is not null)
                    {
                        existing.Position = 0;
                        await StreamSegments.CopyExactlyAsync(existing, staged, plan.Offset, left, cancellation.Token).ConfigureAwait(false);
                    }
                    else existing.Position = plan.OriginalEnd;
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
                    if (staged is not null)
                        await StreamSegments.CopyExactlyAsync(existing, staged, plan.OriginalEnd - plan.Offset, left, cancellation.Token).ConfigureAwait(false);
                    else
                        await StreamSegments.RotateAsync(existing, plan.Offset, plan.OriginalEnd, left, right!).ConfigureAwait(false);
                    await output.FlushAsync(cancellation.Token).ConfigureAwait(false);
                }
            }
            if (count == 0) { token.ThrowIfCancellationRequested(); return 0; }
            if (temporary is not null)
            {
                token.ThrowIfCancellationRequested();
                files.Replace(temporary, destination);
            }
            return count;
        }
        finally { if (owned && temporary is not null) files.DeleteTemporary(temporary); }
    }
}
