using System.Collections.Concurrent;
using System.Text;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class AsyncSourceTests
{
    private static ProbeStream Numbers(bool asyncOnly = false, bool yieldReads = false) =>
        new("Value\n1\n2\n3"u8.ToArray(), asyncOnly: asyncOnly, yieldReads: yieldReads);

    [Fact]
    public async Task SuppliedStreamSupportsTrueAsyncReadsWithoutSyncIo()
    {
        await using var stream = Numbers(asyncOnly: true, yieldReads: true);
        var rows = await TabularFile.Csv<NumberRow>(stream).WithBufferSize(1).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Value));
        Assert.Equal(0, stream.SyncReads);
        Assert.True(stream.AsyncReads > 0);
        Assert.False(stream.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncDisposalRespectsLeaveOpen(bool leaveOpen)
    {
        var stream = Numbers(asyncOnly: true);
        var rows = await TabularFile.Csv<NumberRow>(stream, leaveOpen).ToArrayAsync();
        Assert.Equal(3, rows.Length);
        Assert.Equal(0, stream.SyncDisposals);
        Assert.Equal(leaveOpen ? 0 : 1, stream.AsyncDisposals);
        await stream.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyncDisposalRespectsLeaveOpen(bool leaveOpen)
    {
        var stream = Numbers();
        Assert.Equal(3, TabularFile.Csv<NumberRow>(stream, leaveOpen).Count());
        Assert.Equal(leaveOpen ? 0 : 1, stream.SyncDisposals);
        Assert.Equal(0, stream.AsyncDisposals);
        stream.Dispose();
    }

    [Fact]
    public async Task DirectAwaitForeachAndExplicitAsyncLinqBothWork()
    {
        var rows = new List<int>();
        await foreach (var row in TabularFile.Csv<NumberRow>(() => Numbers(asyncOnly: true)))
            rows.Add(row.Value);
        Assert.Equal(new[] { 1, 2, 3 }, rows);
        var source = TabularFile.Csv<NumberRow>(() => Numbers());
        var synchronous = source.Where(r => r.Value == 2).ToArray();
        Assert.Single(synchronous);
        var doubled = await source.AsAsyncEnumerable().Where(r => r.Value >= 2).Select(r => r.Value * 2).ToArrayAsync();
        Assert.Equal(new[] { 4, 6 }, doubled);
    }

    [Fact]
    public async Task DictionaryMaterializersSupportValuesComparersAndDuplicateErrors()
    {
        var stream = new ProbeStream("Text\nAda\nGrace"u8.ToArray(), asyncOnly: true);
        var rows = await TabularFile.Csv<TextRow>(stream, leaveOpen: false)
            .ToDictionaryAsync(r => r.Text, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Ada", rows["ADA"].Text);
        Assert.Equal(1, stream.AsyncDisposals);

        var values = await TabularFile.Csv<NumberRow>(() => Numbers(asyncOnly: true))
            .ToDictionaryAsync(r => r.Value, r => r.Value * 10);
        Assert.Equal(20, values[2]);

        var duplicate = new ProbeStream("Text\nAda\nADA"u8.ToArray(), asyncOnly: true);
        var duplicateError = await Assert.ThrowsAsync<ArgumentException>(() => TabularFile.Csv<TextRow>(duplicate, false)
            .ToDictionaryAsync(r => r.Text, StringComparer.OrdinalIgnoreCase).AsTask());
        Assert.DoesNotContain("Ada", duplicateError.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, duplicate.AsyncDisposals);
    }

    [Fact]
    public async Task InvalidSelectorsDoNotClaimOrReadSuppliedStream()
    {
        await using var stream = Numbers();
        var source = TabularFile.Csv<NumberRow>(stream);
        Assert.Throws<ArgumentNullException>(() => source.ToDictionaryAsync<int>(null!));
        Assert.Throws<ArgumentNullException>(() => source.ToDictionaryAsync<int, int>(r => r.Value, null!));
        Assert.Equal(0, stream.SyncReads + stream.AsyncReads);
        Assert.Equal(3, (await source.ToArrayAsync()).Length);
    }

    [Fact]
    public async Task CreatingAndDisposingEnumeratorsDoesNotClaimTheStream()
    {
        await using var stream = Numbers();
        var source = TabularFile.Csv<NumberRow>(stream);
        source.GetEnumerator().Dispose();
        await source.GetAsyncEnumerator().DisposeAsync();
        Assert.Equal(0, stream.SyncReads + stream.AsyncReads);
        Assert.Equal(3, source.ToArray().Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ToArrayAsync().AsTask());
    }

    [Fact]
    public async Task AllConfigurationsShareOneEnumerationAllowanceAcrossModes()
    {
        await using var stream = Numbers();
        var source = TabularFile.Csv<NumberRow>(stream);
        var derived = source.WithFieldTrimming().WithBufferSize(1);
        Assert.Equal(3, derived.ToArray().Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ToListAsync().AsTask());
    }

    [Fact]
    public async Task SuppliedStreamRaceHasExactlyOneSuccessfulEnumeration()
    {
        await using var stream = Numbers(yieldReads: true);
        var source = TabularFile.Csv<NumberRow>(stream);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> ReadAsync()
        {
            await start.Task;
            try { await source.WithBufferSize(1).ToArrayAsync(); return true; }
            catch (InvalidOperationException) { return false; }
        }
        async Task<bool> ReadSync()
        {
            await start.Task;
            try { source.WithFieldTrimming().ToArray(); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var asyncRead = Task.Run(ReadAsync);
        var syncRead = Task.Run(ReadSync);
        start.SetResult();
        var results = await Task.WhenAll(asyncRead, syncRead);
        Assert.Single(results, success => success);
    }

    [Fact]
    public async Task AsyncFactoriesAreDeferredRepeatableAndNeverBlockedSynchronously()
    {
        int calls = 0;
        var streams = new List<ProbeStream>();
        var source = TabularFile.CsvAsync<NumberRow>(async token =>
        {
            calls++;
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            var stream = Numbers(asyncOnly: true);
            streams.Add(stream);
            return stream;
        });
        using (var enumerator = source.GetEnumerator())
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Equal(0, calls);
        Assert.Equal(3, (await source.ToArrayAsync()).Length);
        Assert.Equal(3, (await source.ToArrayAsync()).Length);
        Assert.Equal(2, calls);
        Assert.All(streams, stream =>
        {
            Assert.Equal(1, stream.AsyncDisposals);
            Assert.Equal(0, stream.SyncReads);
        });
    }

    [Fact]
    public async Task IndependentFactoryEnumerationsCanOverlap()
    {
        var streams = new ConcurrentBag<ProbeStream>();
        int entered = 0;
        var bothReading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = TabularFile.Csv<NumberRow>(() =>
        {
            int firstRead = 0;
            var stream = new ProbeStream("Value\n1\n2\n3"u8.ToArray(), asyncOnly: true)
            {
                BeforeReadAsync = async token =>
                {
                    if (Interlocked.Exchange(ref firstRead, 1) == 0 && Interlocked.Increment(ref entered) == 2)
                        bothReading.TrySetResult();
                    await bothReading.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                }
            };
            streams.Add(stream);
            return stream;
        });
        var results = await Task.WhenAll(source.ToArrayAsync().AsTask(), source.ToArrayAsync().AsTask());
        Assert.All(results, rows => Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Value)));
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.Equal(1, stream.AsyncDisposals));
    }

    [Fact]
    public async Task PathsSupportIndependentSyncAndAsyncEnumerations()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "Value\n1\n2\n3");
            var source = TabularFile.Csv<NumberRow>(path);
            using var sync = source.GetEnumerator();
            await using var asyncReader = source.GetAsyncEnumerator();
            Assert.True(sync.MoveNext());
            Assert.True(await asyncReader.MoveNextAsync());
            Assert.Equal(1, sync.Current.Value);
            Assert.Equal(1, asyncReader.Current.Value);
            var rows = await source.ToArrayAsync();
            Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Value));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task StreamStartsAtCurrentPositionWithoutSeeking()
    {
        var bytes = Encoding.UTF8.GetBytes("prefixValue\n42");
        await using var stream = new ProbeStream(bytes, chunkSize: 1, asyncOnly: true, initialOffset: 6);
        Assert.Equal(42, (await TabularFile.Csv<NumberRow>(stream).ToArrayAsync()).Single().Value);
    }

    [Theory]
    [InlineData("Other\n1", "schema")]
    [InlineData("Value\nnot-a-number", "conversion")]
    [InlineData("Value\n\"bad", "format")]
    public async Task FailuresDisposeOwnedSourcesAndConsumeAllowance(string csv, string kind)
    {
        var stream = new ProbeStream(Encoding.UTF8.GetBytes(csv), asyncOnly: true);
        var source = TabularFile.Csv<NumberRow>(stream, leaveOpen: false);
        var error = await Assert.ThrowsAnyAsync<TabularFileException>(() => source.ToListAsync().AsTask());
        if (kind == "schema") Assert.IsType<TabularSchemaException>(error);
        if (kind == "conversion") Assert.IsType<TabularConversionException>(error);
        if (kind == "format") Assert.IsType<CsvFormatException>(error);
        Assert.Equal(1, stream.AsyncDisposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ToListAsync().AsTask());
    }

    [Fact]
    public async Task EarlyExitAndSelectorErrorsDisposeOwnedStreams()
    {
        var early = Numbers(asyncOnly: true);
        await foreach (var row in TabularFile.Csv<NumberRow>(early, false))
        {
            Assert.Equal(1, row.Value);
            break;
        }
        Assert.Equal(1, early.AsyncDisposals);

        var throwing = Numbers(asyncOnly: true);
        await Assert.ThrowsAsync<ArithmeticException>(() => TabularFile.Csv<NumberRow>(throwing, false)
            .AsAsyncEnumerable().Select<NumberRow, int>(_ => throw new ArithmeticException()).ToListAsync().AsTask());
        Assert.Equal(1, throwing.AsyncDisposals);
    }

    [Fact]
    public async Task FactoryValidationIsImmediateButReturnedStreamValidationIsDeferred()
    {
        Assert.Throws<ArgumentNullException>(() => TabularFile.Csv<NumberRow>((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => TabularFile.Csv<NumberRow>((Func<Stream>)null!));
        Assert.Throws<ArgumentNullException>(() => TabularFile.CsvAsync<NumberRow>(null!));
        var source = TabularFile.Csv<NumberRow>(() => null!);
        Assert.Throws<InvalidOperationException>(() => source.ToArray());
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ToArrayAsync().AsTask());
        var asyncSource = TabularFile.CsvAsync<NumberRow>(_ => new ValueTask<Stream>((Stream)null!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => asyncSource.ToArrayAsync().AsTask());
    }

    [Fact]
    public async Task UnreadableFactoryStreamsAreDisposedButInvalidSuppliedStreamsStayCallerOwned()
    {
        using var supplied = new UnreadableStream();
        Assert.Throws<ArgumentException>(() => TabularFile.Csv<NumberRow>(supplied, false));
        Assert.False(supplied.Closed);
        var synchronous = new UnreadableStream();
        Assert.Throws<InvalidOperationException>(() => TabularFile.Csv<NumberRow>(() => synchronous).ToArray());
        Assert.True(synchronous.Closed);
        var asynchronous = new UnreadableStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => TabularFile.Csv<NumberRow>(() => asynchronous).ToArrayAsync().AsTask());
        Assert.True(asynchronous.Closed);
    }

    [Fact]
    public async Task DictionaryValueSelectorErrorsDisposeOwnedStreams()
    {
        var stream = Numbers(asyncOnly: true);
        await Assert.ThrowsAsync<ArithmeticException>(() => TabularFile.Csv<NumberRow>(stream, false)
            .ToDictionaryAsync<int, int>(r => r.Value, _ => throw new ArithmeticException()).AsTask());
        Assert.Equal(1, stream.AsyncDisposals);
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override bool CanRead => false;
        internal bool Closed { get; private set; }
        protected override void Dispose(bool disposing) { Closed = true; base.Dispose(disposing); }
    }
}
