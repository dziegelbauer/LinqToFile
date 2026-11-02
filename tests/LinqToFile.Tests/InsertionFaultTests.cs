using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Writing;
using Xunit;

namespace LinqToFile.Tests;

public sealed class InsertionFaultTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-InsertFault-" + Guid.NewGuid().ToString("N"));
    public InsertionFaultTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FaultsBeforeAndAfterEveryIoAndCommitLeaveTheDocumentedState(bool inPlace, bool asynchronous)
    {
        const string original = "Value\nfirst\nlast";
        const string expected = "Value\ninsert\nfirst\nlast";
        string path = Path.Combine(directory, "data.csv");
        var options = new CsvOptions { InPlaceWrites = inPlace };
        async Task<long> Run(FaultFiles files)
        {
            WriteText[] rows = [new() { Value = "insert" }];
            return asynchronous
                ? await CsvInsertion<WriteText>.InsertAsync(path, options, 0, rows, null, default, files)
                : CsvInsertion<WriteText>.Insert(path, options, 0, rows, files);
        }
        File.WriteAllText(path, original);
        var baseline = new FaultFiles(inPlace, asynchronous);
        Assert.Equal(1, await Run(baseline));
        Assert.Equal(expected, File.ReadAllText(path));
        int operations = baseline.Count;
        Assert.True(operations > 5);

        for (int fault = 1; fault <= operations; fault++)
        foreach (bool after in new[] { false, true })
        {
            File.WriteAllText(path, original);
            var files = new FaultFiles(inPlace, asynchronous, fault, after);
            await Assert.ThrowsAsync<IOException>(async () => await Run(files));
            byte[] result = File.ReadAllBytes(path);
            if (!inPlace) Assert.Equal(files.Committed ? expected : original, Encoding.UTF8.GetString(result));
            else
            {
                Assert.Equal(Encoding.UTF8.GetBytes("Value\n"), result.Take(6));
                if (!files.OriginalWritten) Assert.Equal(original, Encoding.UTF8.GetString(result));
                Assert.True(result.Length >= Encoding.UTF8.GetByteCount(original));
            }
            Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
            // Reopen the same path through the public API to prove handle and guard release.
            File.WriteAllText(path, original);
            Assert.Equal(1, TabularFile.Csv<WriteText>(path).Insert(0, new() { Value = "retry" }));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtActualFirstWriteFollowsTheMutationPolicy(bool inPlace)
    {
        const string original = "Value\nfirst\nlast";
        string path = Path.Combine(directory, "data.csv");
        File.WriteAllText(path, original);
        using var cancellation = new CancellationTokenSource();
        var files = new FaultFiles(inPlace, true) { CancelOnWrite = cancellation };
        var task = CsvInsertion<WriteText>.InsertAsync(path, new CsvOptions { InPlaceWrites = inPlace }, 0,
            [new() { Value = "insert" }], null, cancellation.Token, files);
        if (inPlace)
        {
            Assert.Equal(1, await task);
            Assert.Equal("Value\ninsert\nfirst\nlast", File.ReadAllText(path));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
            Assert.Equal(original, File.ReadAllText(path));
        }
        Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringCommitDoesNotTurnSuccessfulReplacementIntoCancellation(bool cancelAfter)
    {
        string path = Path.Combine(directory, "data.csv");
        File.WriteAllText(path, "Value\nfirst\nlast");
        using var cancellation = new CancellationTokenSource();
        var files = new FaultFiles(false, true) { CancelOnCommit = cancellation, CancelAfterCommit = cancelAfter };
        Assert.Equal(1, await CsvInsertion<WriteText>.InsertAsync(path, new CsvOptions(), 0,
            [new() { Value = "insert" }], null, cancellation.Token, files));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal("Value\ninsert\nfirst\nlast", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
    }

    private sealed class FaultFiles(bool inPlace, bool asyncOnly, int failAt = 0, bool failAfter = false) : IWriteFileSystem
    {
        internal int Count { get; private set; }
        internal bool Committed { get; private set; }
        internal bool OriginalWritten { get; set; }
        internal CancellationTokenSource? CancelOnWrite { get; init; }
        internal CancellationTokenSource? CancelOnCommit { get; init; }
        internal bool CancelAfterCommit { get; init; }
        private bool writeCancelled;
        internal int Before()
        {
            int operation = ++Count;
            if (operation == failAt && !failAfter) throw new IOException("Injected I/O failure.");
            return operation;
        }
        internal void After(int operation)
        {
            if (operation == failAt && failAfter) throw new IOException("Injected I/O failure.");
        }
        internal void Written(bool original)
        {
            OriginalWritten |= original;
            if (!writeCancelled && CancelOnWrite is not null)
            {
                writeCancelled = true; CancelOnWrite.Cancel();
            }
        }
        public Stream OpenExisting(string path, bool direct, bool asynchronous) =>
            new FaultStream(PhysicalWriteFileSystem.Instance.OpenExisting(path, direct, asynchronous), this, inPlace, asyncOnly);
        public Stream OpenStaged(string path, Stream original, bool asynchronous) =>
            new FaultStream(PhysicalWriteFileSystem.Instance.OpenStaged(path, ((FaultStream)original).Inner, asynchronous), this, false, asyncOnly);
        public void Replace(string staged, string destination)
        {
            int operation = Before();
            if (!CancelAfterCommit) CancelOnCommit?.Cancel();
            PhysicalWriteFileSystem.Instance.Replace(staged, destination);
            Committed = true;
            if (CancelAfterCommit) CancelOnCommit?.Cancel();
            After(operation);
        }
        public void DeleteTemporary(string path) => PhysicalWriteFileSystem.Instance.DeleteTemporary(path);
    }

    private sealed class FaultStream(Stream inner, FaultFiles faults, bool original, bool asyncOnly) : Stream
    {
        internal Stream Inner => inner;
        public override bool CanRead => inner.CanRead;
        public override bool CanWrite => inner.CanWrite;
        public override bool CanSeek => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(Span<byte> buffer)
        {
            if (asyncOnly) throw new InvalidOperationException("Synchronous reads are forbidden.");
            int operation = faults.Before(); int count = inner.Read(buffer); faults.After(operation); return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int operation = faults.Before();
            int count = await inner.ReadAsync(buffer, cancellationToken);
            faults.After(operation); return count;
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (asyncOnly) throw new InvalidOperationException("Synchronous writes are forbidden.");
            if (buffer.IsEmpty) return;
            int operation = faults.Before(); inner.Write(buffer); faults.Written(original); faults.After(operation);
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty) return;
            int operation = faults.Before();
            await inner.WriteAsync(buffer, cancellationToken);
            faults.Written(original); faults.After(operation);
        }
        public override void Flush()
        {
            if (asyncOnly) throw new InvalidOperationException("Synchronous flush is forbidden.");
            int operation = faults.Before(); inner.Flush(); faults.After(operation);
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            int operation = faults.Before(); await inner.FlushAsync(cancellationToken); faults.After(operation);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                int operation;
                try { operation = faults.Before(); }
                finally { inner.Dispose(); }
                faults.After(operation);
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            int operation;
            try { operation = faults.Before(); }
            finally { await inner.DisposeAsync(); }
            faults.After(operation);
            GC.SuppressFinalize(this);
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new InvalidOperationException("Insertion must not truncate the original.");
    }
}
