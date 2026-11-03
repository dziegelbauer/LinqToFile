using LinqToFile.Writing;

namespace LinqToFile.Formats.Xlsx;

internal static class XlsxMutation<T> where T : class, ITabularRecord<T>
{
    internal static long Write(string path, XlsxOptions options, long? position, IEnumerable<T> rows)
    {
        RequireTemporaryFiles(options);
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        string temporary = TemporaryPath(destination);
        bool owned = false;
        try
        {
            long count = 0;
            using (var original = Open(destination, false))
            using (var prepared = XlsxMutationPreparation<T>.Open(original, options, position))
            using (var input = rows.GetEnumerator())
            {
                if (!input.MoveNext()) return 0;
                var spool = prepared.Package.Resources.CreateTemporary();
                using var quota = new XlsxMutationOutputStream(spool, prepared.Package.Resources);
                var encoded = new EncodedOutput(quota, XlsxDocument.Utf8);
                do
                {
                    CheckRows(prepared, checked(count + 1));
                    prepared.Output.Row(input.Current, count + 1, prepared.InsertionRow + count);
                    encoded.Write(prepared.Output.Record, count + 1);
                    count++;
                } while (input.MoveNext());
                encoded.Write(ReadOnlyMemory<char>.Empty, count, true);
                spool.Flush(); spool.Position = 0;
                using var target = StagedFile.Open(temporary, original, false);
                owned = true;
                using var output = new XlsxMutationOutputStream(target, prepared.Package.Resources, options.MaxWorkbookBytes);
                XlsxMutationArchive.Copy(prepared.Package, prepared.Metadata, output, spool, prepared.InsertionRow, count, prepared.LastRow, prepared.MaxColumn);
            }
            File.Replace(temporary, destination, null);
            owned = false;
            return count;
        }
        finally { if (owned) File.Delete(temporary); }
    }

    internal static async ValueTask<long> WriteAsync(string path, XlsxOptions options, long? position,
        IEnumerable<T>? rows, IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        RequireTemporaryFiles(options);
        token.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        string temporary = TemporaryPath(destination);
        bool owned = false;
        try
        {
            long count = 0;
            await using (var original = Open(destination, true))
            await using (var prepared = await XlsxMutationPreparation<T>.OpenAsync(original, options, position, token).ConfigureAwait(false))
            await using (var input = new WriteInput<T>(rows, asyncRows, token))
            {
                if (!await input.MoveNextAsync(token).ConfigureAwait(false)) { token.ThrowIfCancellationRequested(); return 0; }
                var spool = prepared.Package.Resources.CreateTemporary();
                using var quota = new XlsxMutationOutputStream(spool, prepared.Package.Resources);
                var encoded = new EncodedOutput(quota, XlsxDocument.Utf8);
                do
                {
                    token.ThrowIfCancellationRequested();
                    CheckRows(prepared, checked(count + 1));
                    prepared.Output.Row(input.Current, count + 1, prepared.InsertionRow + count);
                    await encoded.WriteAsync(prepared.Output.Record, count + 1, token).ConfigureAwait(false);
                    count++;
                } while (await input.MoveNextAsync(token).ConfigureAwait(false));
                await encoded.WriteAsync(ReadOnlyMemory<char>.Empty, count, token, true).ConfigureAwait(false);
                await spool.FlushAsync(token).ConfigureAwait(false); spool.Position = 0;
                token.ThrowIfCancellationRequested();
                await using var target = StagedFile.Open(temporary, original, true);
                owned = true;
                using var output = new XlsxMutationOutputStream(target, prepared.Package.Resources, options.MaxWorkbookBytes);
                await XlsxMutationArchive.CopyAsync(prepared.Package, prepared.Metadata, output, spool,
                    prepared.InsertionRow, count, prepared.LastRow, prepared.MaxColumn, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            File.Replace(temporary, destination, null);
            owned = false;
            return count;
        }
        finally { if (owned) File.Delete(temporary); }
    }

    private static void CheckRows(XlsxMutationPreparation<T> prepared, long count)
    {
        if (count > 1_048_576L - prepared.LastRow)
            throw new XlsxFormatException("The worksheet row limit would be exceeded.")
            { WorksheetName = prepared.Package.WorksheetName, PartName = prepared.Package.WorksheetPart };
    }
    private static void RequireTemporaryFiles(XlsxOptions options)
    {
        if (!options.TemporaryFiles) throw new XlsxFormatException("Workbook mutation requires temporary files.");
    }
    private static FileStream Open(string path, bool asynchronous) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
        4096, FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
    private static string TemporaryPath(string destination) => Path.Combine(Path.GetDirectoryName(destination)!, ".linqtofile-" + Guid.NewGuid().ToString("N") + ".tmp");
}
