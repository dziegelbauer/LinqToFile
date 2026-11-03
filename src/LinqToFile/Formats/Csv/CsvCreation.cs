using LinqToFile.Writing;

namespace LinqToFile.Formats.Csv;

internal static class CsvCreation<T> where T : class, ITabularRecord<T>
{
    private static void ValidateDestination(string destination, CsvOptions options)
    {
        if (options.InPlaceWrites && options.ExistingFileBehavior == ExistingFileBehavior.Overwrite)
            throw new InvalidOperationException("Overwrite creation requires staged writes.");
        if (Directory.Exists(destination) ||
            (options.ExistingFileBehavior == ExistingFileBehavior.Throw && File.Exists(destination)))
            throw new IOException("The creation destination already exists.");
    }
    private static string TemporaryPath(string destination) =>
        Path.Combine(Path.GetDirectoryName(destination)!, ".linqtofile-" + Guid.NewGuid().ToString("N") + ".tmp");

    private static FileStream Open(string target, string destination, CsvOptions options, bool asynchronous)
    {
        var settings = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 4096,
            Options = FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None)
        };
        if (!OperatingSystem.IsWindows() && options.ExistingFileBehavior == ExistingFileBehavior.Overwrite && File.Exists(destination))
            settings.UnixCreateMode = File.GetUnixFileMode(destination) &
                (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                 UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        return new FileStream(target, settings);
    }

    internal static long Create(string path, CsvOptions options, IEnumerable<T> rows)
    {
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        ValidateDestination(destination, options);
        var output = new CsvOutput<T>(options); // Validate the model and header before opening any file.
        string target = options.InPlaceWrites ? destination : TemporaryPath(destination);
        bool owned = false;
        try
        {
            long count = 0;
            using (var stream = Open(target, destination, options, false))
            {
                owned = true;
                var encoded = new EncodedOutput(stream, options.Encoding);
                stream.Write(options.Encoding.GetPreamble());
                encoded.Write(output.Record, 0);
                foreach (var row in rows)
                {
                    output.Row(row, checked(count + 1));
                    encoded.Write(output.Record, count + 1);
                    count++;
                }
                encoded.Write(ReadOnlyMemory<char>.Empty, count, true);
            }
            if (!options.InPlaceWrites) File.Move(target, destination, options.ExistingFileBehavior == ExistingFileBehavior.Overwrite);
            return count;
        }
        finally { if (owned && !options.InPlaceWrites && File.Exists(target)) File.Delete(target); }
    }

    internal static async ValueTask<long> CreateAsync(string path, CsvOptions options, IEnumerable<T>? rows,
        IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(path);
        using var guard = WriteGuard.Acquire(destination);
        ValidateDestination(destination, options);
        var output = new CsvOutput<T>(options);
        string target = options.InPlaceWrites ? destination : TemporaryPath(destination);
        bool owned = false;
        try
        {
            long count = 0;
            token.ThrowIfCancellationRequested();
            // CreateNew is the first destination mutation for direct creation.
            CancellationToken writeToken = options.InPlaceWrites ? CancellationToken.None : token;
            await using (var stream = Open(target, destination, options, true))
            {
                owned = true;
                var encoded = new EncodedOutput(stream, options.Encoding);
                await stream.WriteAsync(options.Encoding.GetPreamble(), writeToken).ConfigureAwait(false);
                await encoded.WriteAsync(output.Record, 0, writeToken).ConfigureAwait(false);
                if (asyncRows is not null)
                {
                    await foreach (var row in asyncRows.WithCancellation(writeToken).ConfigureAwait(false))
                    {
                        writeToken.ThrowIfCancellationRequested();
                        output.Row(row, checked(count + 1));
                        await encoded.WriteAsync(output.Record, count + 1, writeToken).ConfigureAwait(false);
                        count++;
                    }
                }
                else
                {
                    using var enumerator = rows!.GetEnumerator();
                    while (true)
                    {
                        writeToken.ThrowIfCancellationRequested();
                        if (!enumerator.MoveNext()) break;
                        output.Row(enumerator.Current, checked(count + 1));
                        await encoded.WriteAsync(output.Record, count + 1, writeToken).ConfigureAwait(false);
                        count++;
                    }
                }
                await encoded.WriteAsync(ReadOnlyMemory<char>.Empty, count, writeToken, true).ConfigureAwait(false);
                await stream.FlushAsync(writeToken).ConfigureAwait(false);
            }
            if (!options.InPlaceWrites)
            {
                token.ThrowIfCancellationRequested();
                File.Move(target, destination, options.ExistingFileBehavior == ExistingFileBehavior.Overwrite);
            }
            return count;
        }
        finally { if (owned && !options.InPlaceWrites && File.Exists(target)) File.Delete(target); }
    }
}
