using LinqToFile;
using SampleModels;

internal static class WorkbookAsyncSample
{
    internal static async Task RunAsync()
    {
        await RunPathAsync();
        foreach (bool strict in new[] { false, true })
        {
            Stream Open() => new ForwardOnly(WorkbookCellsSample.Create(strict));
            var sync = TabularFile.Xlsx<WorkbookCells>(Open).WithSharedStringMemoryBudget(0);
            if (sync.Single().Name != "Ada Lovelace") throw new InvalidOperationException("XLSX sync spool failed.");

            var source = TabularFile.XlsxAsync<WorkbookCells>(token =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(Open());
            }).WithSharedStringMemoryBudget(0);
            var rows = await source.AsAsyncEnumerable().Where(row => row.Count == 2).ToArrayAsync();
            var dictionary = await source.ToDictionaryAsync(row => row.Name, row => row.Count, StringComparer.Ordinal);
            if (rows.Length != 1 || rows[0].Name != "Ada Lovelace" || dictionary["Ada Lovelace"] != 2 ||
                (await source.ToListAsync()).Count != 1 || (await source.ToArrayAsync()).Length != 1)
                throw new InvalidOperationException("XLSX async query failed.");
        }
        Console.WriteLine("XLSX async: factories, non-seekable input, LINQ, materializers, disk spill");
    }

    private static async Task RunPathAsync()
    {
        string path = Path.GetTempFileName();
        try
        {
            using var fixture = WorkbookSample.Create(false);
            await File.WriteAllBytesAsync(path, fixture.ToArray());
            var entries = TabularFile.Xlsx<WorkbookEntry>(path);
            var activeEntries = await entries.AsAsyncEnumerable()
                .Where(entry => entry.Active)
                .ToArrayAsync();
            var byId = await entries.ToDictionaryAsync(entry => entry.Id);
            if (activeEntries.Length != 1 || byId[42].Name != "Ada")
                throw new InvalidOperationException("XLSX async path query failed.");
        }
        finally { File.Delete(path); }
    }

    private sealed class ForwardOnly(Stream stream) : Stream
    {
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => stream.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => stream.ReadAsync(buffer, token);
        protected override void Dispose(bool disposing) { if (disposing) stream.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => stream.DisposeAsync();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
