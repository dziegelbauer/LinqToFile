using System.IO.Compression;
using LinqToFile.Tests.Support;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxSourceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SyncAsyncParityIncludesBothVariantsNonSeekableInputAndDiskSpill(bool strict, bool spill)
    {
        using var book = Create(Row(1, Cell("A1", "s", "0")) + Row(2, Cell("A2", "s", "1")) +
            Row(3, "<c r=\"A3\" t=\"str\"><f>ignored</f><v>_x0042_</v></c>"), strict,
            change: p => SharedStrings(p, "<si><t>Value</t></si><si><r><t>A</t></r><r><t>da</t></r></si>", strict));
        byte[] bytes = book.ToArray();
        string directory = NewDirectory();
        try
        {
            using var sync = new ProbeStream(bytes, chunkSize: 7);
            Assert.Equal(new[] { "Ada", "B" }, Configure(TabularFile.Xlsx<SheetString>(sync)).Select(r => r.Value));
            await using var asyncInput = new ProbeStream(bytes, chunkSize: 7, asyncOnly: true, yieldReads: true);
            Assert.Equal(new[] { "Ada", "B" }, (await Configure(TabularFile.Xlsx<SheetString>(asyncInput)).ToArrayAsync()).Select(r => r.Value));
            Assert.Equal(0, asyncInput.SyncReads);
            Assert.True(asyncInput.AsyncReads > 0);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }

        XlsxSource<SheetString> Configure(XlsxSource<SheetString> source) => source
            .WithTemporaryDirectory(directory).WithSharedStringMemoryBudget(spill ? 0 : 8192);
    }

    [Fact]
    public async Task SeekableAsyncInputUsesSyncReadsOnlyForTheZipDirectoryAndKeepsItsOrigin()
    {
        using var book = Create(SingleColumn("42"));
        var bytes = new byte[book.Length + 19];
        book.ToArray().CopyTo(bytes, 19);
        await using var stream = new AsyncSeekStream(bytes, 19);
        var rows = await TabularFile.Xlsx<SheetInteger>(stream).WithTemporaryFiles(false).ToListAsync();
        Assert.Equal(42, Assert.Single(rows).Value);
        Assert.True(stream.AsyncReads > 0);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task FactoriesAreDeferredRepeatableAndIndependentAcrossConcurrentEnumerations()
    {
        using var book = Create(SingleColumn("42"));
        byte[] bytes = book.ToArray();
        var opened = new List<ProbeStream>();
        var source = TabularFile.Xlsx<SheetInteger>(() => { var s = new ProbeStream(bytes); opened.Add(s); return s; });
        Assert.Empty(opened);
        Assert.Equal(42, source.Single().Value);
        Assert.Equal(42, Assert.Single(await source.ToArrayAsync()).Value);
        Assert.All(opened, s => Assert.True(s.IsDisposed));
        int calls = 0;
        var asyncSource = TabularFile.XlsxAsync<SheetInteger>(async token =>
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            return new ProbeStream(bytes, asyncOnly: true, yieldReads: true);
        });
        Assert.Throws<InvalidOperationException>(() => asyncSource.Single());
        Assert.Equal(0, calls);
        var results = await Task.WhenAll(asyncSource.ToArrayAsync().AsTask(), asyncSource.ToArrayAsync().AsTask());
        Assert.All(results, r => Assert.Equal(42, Assert.Single(r).Value));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedStreamClaimsAreSharedAcrossSyncAsyncAndOptions(bool leaveOpen)
    {
        using var book = Create(SingleColumn("42"));
        var stream = new ProbeStream(book.ToArray(), asyncOnly: true);
        var source = TabularFile.Xlsx<SheetInteger>(stream, leaveOpen);
        await using (var unused = source.GetAsyncEnumerator()) { }
        Assert.Equal(42, Assert.Single(await source.WithWorksheet("Data").ToArrayAsync()).Value);
        Assert.Throws<InvalidOperationException>(() => source.Single());
        Assert.Equal(!leaveOpen, stream.IsDisposed);
        Assert.Equal(leaveOpen ? 0 : 1, stream.AsyncDisposals);
        stream.Dispose();
    }

    [Fact]
    public async Task PreCancelledMoveClaimsAndDisposesOwnedInputWithoutReading()
    {
        using var book = Create(SingleColumn("42"));
        var stream = new ProbeStream(book.ToArray(), asyncOnly: true);
        var source = TabularFile.Xlsx<SheetInteger>(stream, leaveOpen: false);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.ToArrayAsync(cancel.Token));
        Assert.Equal(0, stream.AsyncReads);
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Throws<InvalidOperationException>(() => source.Single());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpoolLimitsIncludeWorkbookAndSharedStringFiles(bool async)
    {
        using var book = Create(SingleColumn("42"), change: p => SharedStrings(p, "<si><t>abc</t></si>"));
        byte[] bytes = book.ToArray();
        string directory = NewDirectory();
        try
        {
            foreach (string mode in new[] { "exact", "workbook", "temporary", "forbidden" })
            {
                using var stream = new ProbeStream(bytes, chunkSize: 3, asyncOnly: async);
                var source = TabularFile.Xlsx<SheetInteger>(stream).WithSharedStringMemoryBudget(0)
                    .WithTemporaryDirectory(directory).WithMaxWorkbookBytes(mode == "workbook" ? bytes.Length - 1 : bytes.Length)
                    .WithMaxTemporaryBytes(bytes.Length + (mode == "temporary" ? 17 : 18)).WithTemporaryFiles(mode != "forbidden");
                if (mode == "exact")
                {
                    var rows = async ? await source.ToArrayAsync() : source.ToArray();
                    Assert.Equal(42, Assert.Single(rows).Value);
                }
                else if (async) await Assert.ThrowsAsync<XlsxFormatException>(async () => await source.ToArrayAsync());
                else Assert.Throws<XlsxFormatException>(() => source.ToArray());
                if (mode == "forbidden") Assert.Equal(0, stream.SyncReads + stream.AsyncReads);
                Assert.Empty(Directory.GetFiles(directory));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("setup")]
    [InlineData("rows")]
    [InlineData("early")]
    [InlineData("conversion")]
    public async Task CancellationFailureAndEarlyExitRemoveAllTemporaryFiles(string phase)
    {
        using var book = Create(SingleColumn(phase == "conversion" ? "1.5" : "42"),
            change: p => SharedStrings(p, "<si><t>unused</t></si>"));
        string directory = NewDirectory();
        using var cancel = new CancellationTokenSource();
        try
        {
            Stream input = phase == "setup"
                ? new AsyncSeekStream(book.ToArray(), 0, () => cancel.Cancel())
                : new ProbeStream(book.ToArray(), asyncOnly: true)
                { BeforeReadAsync = _ => { if (phase == "copy") cancel.Cancel(); return default; } };
            var source = TabularFile.Xlsx<SheetInteger>(input, leaveOpen: false)
                .WithTemporaryDirectory(directory).WithSharedStringMemoryBudget(0);
            await using (var iterator = source.GetAsyncEnumerator(cancel.Token))
            {
                if (phase is "copy" or "setup")
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await iterator.MoveNextAsync());
                else if (phase == "conversion")
                    await Assert.ThrowsAsync<TabularConversionException>(async () => await iterator.MoveNextAsync());
                else
                {
                    Assert.True(await iterator.MoveNextAsync());
                    Assert.NotEmpty(Directory.GetFiles(directory));
                    if (phase == "rows")
                    {
                        cancel.Cancel();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await iterator.MoveNextAsync());
                    }
                }
            }
            Assert.False(input.CanRead);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task AllAsyncMaterializersAndLinqPreserveKeysValuesAndDuplicatePrivacy()
    {
        using var book = Create(SingleColumn("42") + Row(3, Number("A3", "7")));
        var source = TabularFile.Xlsx<SheetInteger>(() => new MemoryStream(book.ToArray()));
        Assert.Equal(2, (await source.ToListAsync()).Count);
        Assert.Equal(42, (await source.ToDictionaryAsync(r => r.Value))[42].Value);
        Assert.Equal("42", (await source.ToDictionaryAsync(r => r.Value, r => r.Value.ToString()))[42]);
        var names = await source.ToDictionaryAsync(r => r.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, names.Count);
        Assert.Equal(42, Assert.Single(await source.AsAsyncEnumerable().Where(r => r.Value > 10).ToArrayAsync()).Value);
        var error = await Assert.ThrowsAsync<ArgumentException>(async () => await source.ToDictionaryAsync(_ => "secret-key"));
        Assert.DoesNotContain("secret-key", error.ToString());
    }

    [Fact]
    public async Task PathsSupportIndependentConcurrentReads()
    {
        using var book = Create(SingleColumn("42"));
        string directory = NewDirectory();
        string path = Path.Combine(directory, "book.xlsx");
        try
        {
            File.WriteAllBytes(path, book.ToArray());
            var source = TabularFile.Xlsx<SheetInteger>(path);
            var arrays = await Task.WhenAll(source.ToArrayAsync().AsTask(), source.ToArrayAsync().AsTask());
            Assert.All(arrays, rows => Assert.Equal(42, Assert.Single(rows).Value));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "LinqToFile-XlsxSources-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    [Theory]
    [InlineData("shared")]
    [InlineData("preflight")]
    [InlineData("worksheet")]
    public async Task CancellationReachesSharedStringProcessingPreflightAndReopenedXml(string phase)
    {
        string items = string.Concat(Enumerable.Range(0, 2000).Select(_ => "<si><t>" + Guid.NewGuid().ToString("N") + "</t></si>"));
        using var book = Create(SingleColumn("42"), change: p => SharedStrings(p, items));
        byte[] bytes = book.ToArray();
        string part = phase == "shared" ? "xl/sharedStrings.xml" : "xl/worksheets/data.xml";
        int nameOffset = bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(part));
        int local = nameOffset - 30;
        int extra = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(local + 28, 2));
        int content = nameOffset + part.Length + extra;
        int length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(local + 18, 4));
        int starts = 0;
        string directory = NewDirectory();
        using var cancel = new CancellationTokenSource();
        await using var input = new AsyncSeekStream(bytes, 0, onPosition: position =>
        {
            if (position >= content && position < content + length)
            {
                starts++;
                if (phase == "shared" ? starts == 100 : phase == "preflight" || position == content && starts > 1)
                    cancel.Cancel();
            }
        });
        try
        {
            var source = TabularFile.Xlsx<SheetInteger>(input).WithSharedStringMemoryBudget(0).WithTemporaryDirectory(directory);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.ToArrayAsync(cancel.Token));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("1.5", "n", "conversion")]
    [InlineData("secret-invalid", "n", "format")]
    [InlineData("999", "s", "format")]
    [InlineData("#N/A", "e", "format")]
    [InlineData("2026-02-30", "d", "format")]
    public async Task AsyncErrorsPreserveLocationTypeAndPrivacy(string value, string type, string expected)
    {
        using var book = Create(SingleColumn(value, type));
        var source = TabularFile.Xlsx<SheetInteger>(book);
        var error = await Assert.ThrowsAnyAsync<TabularFileException>(async () => await source.ToArrayAsync());
        if (expected == "format") Assert.IsType<XlsxFormatException>(error);
        else Assert.IsType<TabularConversionException>(error);
        Assert.Equal("Data", error.WorksheetName);
        Assert.Equal("A2", error.CellReference);
        Assert.Equal(2, error.WorksheetRowNumber);
        Assert.Equal("xl/worksheets/data.xml", error.PartName);
        Assert.DoesNotContain(value, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncTemporalConversionsMatchSync(bool date1904)
    {
        using var book = Create(SingleColumn("61.5"), date1904, change: p =>
            p["xl/workbook.xml"] = p["xl/workbook.xml"].Replace("<sheets>", $"<workbookPr date1904=\"{(date1904 ? 1 : 0)}\"/><sheets>", StringComparison.Ordinal));
        byte[] bytes = book.ToArray();
        var source = TabularFile.Xlsx<SheetDateTime>(() => new MemoryStream(bytes));
        Assert.Equal(source.Single().Value, Assert.Single(await source.ToArrayAsync()).Value);
        using var iso = Create(SingleColumn("2026-11-01T01:30:45.12345675-07:00", "d"));
        var offset = Assert.Single(await TabularFile.Xlsx<SheetOffset>(iso).ToArrayAsync()).Value;
        Assert.Equal(TimeSpan.FromHours(-7), offset.Offset);
        Assert.Equal(1234568, offset.Ticks % TimeSpan.TicksPerSecond);
    }

    private sealed class AsyncSeekStream(byte[] bytes, int origin, Action? onRead = null, Action<long>? onPosition = null) : Stream
    {
        private readonly MemoryStream inner = new(bytes) { Position = origin };
        // Fixture ZIPs have no ZIP64 records or archive comment.
        private readonly long directoryStart = origin + System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4));
        internal int AsyncReads { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set { Assert.True(value >= origin); inner.Position = value; } }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            Assert.True(inner.Position >= directoryStart, "Synchronous reads must stay within ZIP directory metadata.");
            return inner.Read(buffer);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            AsyncReads++;
            onRead?.Invoke();
            onPosition?.Invoke(inner.Position);
            await Task.Yield();
            return await inner.ReadAsync(buffer[..Math.Min(buffer.Length, 31)], token);
        }
        public override long Seek(long offset, SeekOrigin originKind)
        {
            long result = inner.Seek(offset, originKind);
            Assert.True(result >= origin);
            return result;
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
