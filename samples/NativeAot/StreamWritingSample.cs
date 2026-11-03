using System.IO.Compression;
using LinqToFile;
using SampleModels;

internal static class StreamWritingSample
{
    internal static async Task RunAsync()
    {
        using var stream = new MemoryStream();
        var writer = TabularFile.CsvWriter<InventoryItem>(stream)
            .WithDelimiter(';');
        long written = await writer.WriteAsync(new[]
        {
            new InventoryItem { Id = 1, Available = true, Note = "fresh", ExpiresOn = new DateOnly(2026, 12, 31) }
        });
        stream.Position = 0;
        var rows = await TabularFile.Csv<InventoryItem>(stream)
            .WithDelimiter(';')
            .ToListAsync();
        if (written != 1 || rows.Count != 1 || rows[0].Available != true || rows[0].Note != "fresh" || rows[0].ExpiresOn != new DateOnly(2026, 12, 31))
            throw new InvalidOperationException("Supplied-stream CSV round trip failed.");

        string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Exports-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var exporter = TabularFile.CsvWriter<InventoryItem>(() =>
                File.Create(Path.Combine(directory, Guid.NewGuid() + ".csv")));
            await exporter.WriteAsync(rows);
            await exporter.WriteAsync(rows);
            if (Directory.GetFiles(directory, "*.csv").Length != 2)
                throw new InvalidOperationException("Repeated factory exports failed.");
        }
        finally { Directory.Delete(directory, true); }

        var sink = new AsyncOnlySink();
        var asyncExporter = TabularFile.CsvWriterAsync<InventoryItem>(async token =>
        { await Task.Yield(); token.ThrowIfCancellationRequested(); return sink; });
        if (await asyncExporter.WriteAsync(AsyncRows()) != 2 || !sink.Disposed)
            throw new InvalidOperationException("Async-only destination ownership failed.");
        using var exported = new MemoryStream(sink.Bytes);
        var exportedRows = await TabularFile.Csv<InventoryItem>(exported).ToListAsync();
        if (exportedRows.Count != 2 || exportedRows[0].Available is not null || exportedRows[1].Available != false)
            throw new InvalidOperationException("Async input/encoding stream export failed.");

        using var compressed = new MemoryStream();
        var compressedWriter = TabularFile.CsvWriter<InventoryItem>(() =>
            new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true));
        if (compressedWriter.Write(rows) != 1) throw new InvalidOperationException("Compression export failed.");
        compressed.Position = 0;
        await using var unzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        if ((await TabularFile.Csv<InventoryItem>(unzip).ToListAsync()).Count != 1)
            throw new InvalidOperationException("Owned compression wrapper was not finalized.");
        Console.WriteLine("CSV streams: current-position output, repeatable factories, async-only I/O/input, annotations, and compression passed.");
    }

    private static async IAsyncEnumerable<InventoryItem> AsyncRows()
    {
        await Task.Yield();
        yield return new InventoryItem { Id = 2 };
        yield return new InventoryItem { Id = 3, Available = false, Note = "雪" };
    }

    internal sealed class AsyncOnlySink : Stream
    {
        private readonly MemoryStream storage = new();
        internal bool Disposed { get; private set; }
        internal byte[] Bytes => storage.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !Disposed;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous I/O is forbidden.");
        public override void Flush() => throw new InvalidOperationException("Synchronous I/O is forbidden.");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => storage.WriteAsync(buffer, cancellationToken);
        public override Task FlushAsync(CancellationToken cancellationToken) => storage.FlushAsync(cancellationToken);
        public override ValueTask DisposeAsync()
        { Disposed = true; storage.Dispose(); GC.SuppressFinalize(this); return default; }
    }
}
