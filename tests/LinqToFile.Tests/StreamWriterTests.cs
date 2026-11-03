using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class StreamWriterTests
{
    [Fact]
    public async Task ExportsHeadersSingleRowsAndBothRangeKindsWithoutOwningSuppliedStreams()
    {
        using var header = new ExportProbeStream();
        Assert.Equal(0, TabularFile.CsvWriter<ExportRow>(header).Write());
        Assert.Equal("Value\n", Encoding.UTF8.GetString(header.Bytes));
        Assert.Equal(1, header.SyncFlushes);
        Assert.False(header.IsDisposed);
        using var single = new ExportProbeStream(asyncOnly: true);
        Assert.Equal(1, await TabularFile.CsvWriter<ExportRow>(single).WriteAsync(new ExportRow { Value = 42 }));
        Assert.Equal("Value\n42\n", Encoding.UTF8.GetString(single.Bytes));
        Assert.Equal(0, single.SyncWrites);
        Assert.Equal(0, single.SyncFlushes);
        Assert.Equal(1, single.AsyncFlushes);
        Assert.False(single.IsDisposed);
        using var range = new MemoryStream();
        Assert.Equal(2, TabularFile.CsvWriter<ExportRow>(range).Write(new[] { new ExportRow { Value = 1 }, new ExportRow { Value = 2 } }));
        using var asyncRange = new ExportProbeStream(asyncOnly: true);
        Assert.Equal(2, await TabularFile.CsvWriter<ExportRow>(asyncRange).WriteAsync(AsyncRows(1, 2)));
        Assert.Equal(range.ToArray(), asyncRange.Bytes);
        using var empty = new ExportProbeStream(asyncOnly: true);
        Assert.Equal(0, await TabularFile.CsvWriter<ExportRow>(empty).WriteAsync());
        Assert.Equal("Value\n", Encoding.UTF8.GetString(empty.Bytes));
    }

    [Fact]
    public void WritesAtCurrentPositionWithoutTruncationOrImplicitNewline()
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("prefix--------------------tail"));
        stream.Position = 6;
        Assert.Equal(1, TabularFile.CsvWriter<ExportRow>(stream).Write(new ExportRow { Value = 42 }));
        Assert.Equal("prefixValue\n42\n-----------tail", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    [InlineData(12000)]
    [InlineData(12001)]
    public async Task EmitsExactlyOneRequestedPreambleAtNonzeroAndNonseekablePositions(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        using var seekable = new MemoryStream();
        seekable.Write([1, 2, 3]);
        Assert.Equal(1, TabularFile.CsvWriter<ExportRow>(seekable).WithEncoding(encoding).Write(new ExportRow { Value = 7 }));
        byte[] expected = new byte[] { 1, 2, 3 }.Concat(encoding.GetPreamble()).Concat(encoding.GetBytes("Value\n7\n")).ToArray();
        Assert.Equal(expected, seekable.ToArray());
        using var nonseekable = new ExportProbeStream(asyncOnly: true);
        await TabularFile.CsvWriter<ExportRow>(nonseekable).WithEncoding(encoding).WriteAsync(new ExportRow { Value = 7 });
        Assert.Equal(expected[3..], nonseekable.Bytes);
    }

    [Fact]
    public async Task HonorsDialectCultureAnnotationsAndOutputLimits()
    {
        using var stream = new MemoryStream();
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        var writer = TabularFile.CsvWriter<TokenDate>(stream).WithCulture(culture).WithDelimiter(';').WithQuote('\'').WithNewLine(LineEnding.CrLf);
        culture.DateTimeFormat.DateSeparator = "!";
        Assert.Equal(2, await writer.WriteAsync(new[] { new TokenDate(), new TokenDate { Value = new(2026, 12, 31) } }));
        Assert.Equal("Value\r\nNULL\r\n31/12/2026\r\n", Encoding.UTF8.GetString(stream.ToArray()));
        using var limited = new ExportProbeStream();
        var error = Assert.Throws<TabularWriteException>(() => TabularFile.CsvWriter<TokenText>(limited).WithMaxFieldLength(5).Write(new TokenText { Value = "private-value" }));
        Assert.Equal("Value", error.TargetMember);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private-value", error.ToString(), StringComparison.Ordinal);
        Assert.False(limited.IsDisposed);
        Assert.Equal(0, limited.SyncFlushes);
    }

    [Fact]
    public async Task SingleUseIsSharedAndInvalidArgumentsDoNotConsumeIt()
    {
        using var stream = new ExportProbeStream(asyncOnly: true);
        var writer = TabularFile.CsvWriter<ExportRow>(stream);
        var derived = writer.WithNewLine(LineEnding.CrLf);
        Assert.Throws<ArgumentNullException>(() => writer.Write((IEnumerable<ExportRow>)null!));
        Assert.Throws<ArgumentNullException>(() => writer.WriteAsync((IAsyncEnumerable<ExportRow>)null!));
        Assert.Equal(0, await derived.WriteAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync().AsTask());
        Assert.Equal("Value\r\n", Encoding.UTF8.GetString(stream.Bytes));
    }

    [Fact]
    public async Task OverlappingSuppliedExportsFailBeforeTouchingTheStream()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new ExportProbeStream(asyncOnly: true)
        {
            BeforeAsyncWrite = async _ => { entered.TrySetResult(); await release.Task; }
        };
        var writer = TabularFile.CsvWriter<ExportRow>(stream);
        var first = writer.WriteAsync(new ExportRow { Value = 1 }).AsTask();
        await entered.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WithDelimiter(';').WriteAsync().AsTask());
        release.SetResult();
        Assert.Equal(1, await first);
    }

    [Fact]
    public async Task FactoriesAreRepeatableConcurrentAndDisposeEachIndependentStream()
    {
        var streams = new ConcurrentBag<ExportProbeStream>();
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var writer = TabularFile.CsvWriterAsync<ExportRow>(async token =>
        {
            if (Interlocked.Increment(ref calls) == 2) both.SetResult();
            await both.Task.WaitAsync(token);
            var stream = new ExportProbeStream(asyncOnly: true);
            streams.Add(stream);
            return stream;
        });
        var counts = await Task.WhenAll(writer.WriteAsync(new ExportRow { Value = 1 }).AsTask(),
            writer.WithNewLine(LineEnding.CrLf).WriteAsync(new ExportRow { Value = 2 }).AsTask());
        Assert.Equal(new long[] { 1, 1 }, counts);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => { Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals); Assert.Equal(0, stream.SyncWrites); });
        Assert.Contains(streams, stream => Encoding.UTF8.GetString(stream.Bytes) == "Value\n1\n");
        Assert.Contains(streams, stream => Encoding.UTF8.GetString(stream.Bytes) == "Value\r\n2\r\n");
    }

    [Fact]
    public async Task SyncFactorySupportsBothOperationKindsAndAsyncOnlyFactoryRejectsSync()
    {
        var streams = new List<ExportProbeStream>();
        var writer = TabularFile.CsvWriter<ExportRow>(() => { var stream = new ExportProbeStream(); streams.Add(stream); return stream; });
        Assert.Equal(0, writer.Write());
        Assert.Equal(1, await writer.WriteAsync(new ExportRow { Value = 9 }));
        Assert.Equal(1, streams[0].SyncDisposals);
        Assert.Equal(1, streams[1].AsyncDisposals);
        int calls = 0;
        var asyncOnly = TabularFile.CsvWriterAsync<ExportRow>(_ => { calls++; return new ValueTask<Stream>(new ExportProbeStream(asyncOnly: true)); });
        Assert.Throws<InvalidOperationException>(() => asyncOnly.Write());
        Assert.Equal(0, calls);
        Assert.Equal(0, await asyncOnly.WriteAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedOwnershipRespectsLeaveOpenForBothOperationKinds(bool asynchronous)
    {
        var stream = new ExportProbeStream(asyncOnly: asynchronous);
        var writer = TabularFile.CsvWriter<ExportRow>(stream, leaveOpen: false);
        if (asynchronous) await writer.WriteAsync(); else writer.Write();
        Assert.True(stream.IsDisposed);
        Assert.Equal(asynchronous ? 1 : 0, stream.AsyncDisposals);
        Assert.Equal(asynchronous ? 0 : 1, stream.SyncDisposals);
    }

    [Fact]
    public async Task PrecancelledSuppliedAttemptIsConsumedAndOwnedStreamDisposed()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var stream = new ExportProbeStream(asyncOnly: true);
        var writer = TabularFile.CsvWriter<ExportRow>(stream, leaveOpen: false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(cancellation.Token).AsTask());
        Assert.Empty(stream.Bytes);
        Assert.Equal(1, stream.AsyncDisposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync().AsTask());
        int calls = 0;
        var factory = TabularFile.CsvWriter<ExportRow>(() => { calls++; return new ExportProbeStream(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.WriteAsync(cancellation.Token).AsTask());
        Assert.Equal(0, calls);
        Assert.Equal(0, await factory.WriteAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellationAfterHeaderStopsRowsAndDoesNotFlushOrReplay()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new ExportProbeStream(asyncOnly: true) { AfterWrite = cancellation.Cancel };
        var writer = TabularFile.CsvWriter<ExportRow>(stream);
        bool enumerated = false;
        IEnumerable<ExportRow> Rows() { enumerated = true; yield return new() { Value = 42 }; }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(Rows(), cancellation.Token).AsTask());
        Assert.False(enumerated);
        Assert.Equal("Value\n", Encoding.UTF8.GetString(stream.Bytes));
        Assert.Equal(0, stream.AsyncFlushes);
        Assert.False(stream.IsDisposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync().AsTask());
    }

    [Fact]
    public async Task CancellationReachesAsyncInputAndFinalFlush()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken seen = default;
        async IAsyncEnumerable<ExportRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        { seen = token; await Task.Yield(); yield return new() { Value = 1 }; }
        var stream = new ExportProbeStream(asyncOnly: true) { BeforeAsyncFlush = token => { Assert.Equal(cancellation.Token, token); cancellation.Cancel(); return Task.CompletedTask; } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TabularFile.CsvWriter<ExportRow>(stream, false).WriteAsync(Rows(), cancellation.Token).AsTask());
        Assert.Equal(cancellation.Token, seen);
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(1, stream.AsyncFlushes);
    }

    [Fact]
    public async Task OwnedDisposalCompletesBeforeSuccessAndDisposalFailureIsNotSuccess()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ExportProbeStream(asyncOnly: true) { BeforeAsyncDispose = async () => { entered.SetResult(); await release.Task; } };
        var task = TabularFile.CsvWriter<ExportRow>(stream, false).WriteAsync().AsTask();
        await entered.Task;
        Assert.False(task.IsCompleted);
        release.SetResult();
        Assert.Equal(0, await task);
        var failure = new IOException("dispose failed");
        var bad = new ExportProbeStream { DisposeFailure = failure };
        Assert.Same(failure, Assert.Throws<IOException>(() => TabularFile.CsvWriter<ExportRow>(bad, false).Write()));
    }

    [Fact]
    public async Task ExportErrorsRemainPrimaryAndDoNotTriggerAnExtraFlush()
    {
        var failure = new IOException("write failure");
        var cleanup = new IOException("cleanup failure");
        var stream = new ExportProbeStream(asyncOnly: true) { WriteFailure = failure, DisposeFailure = cleanup };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => TabularFile.CsvWriter<ExportRow>(stream, false).WriteAsync().AsTask()));
        Assert.Equal(0, stream.AsyncFlushes);
        Assert.Equal(1, stream.AsyncDisposals);
        var inputFailure = new InvalidOperationException("enumerator failure");
        IEnumerable<ExportRow> Broken() { yield return new() { Value = 1 }; throw inputFailure; }
        var second = new ExportProbeStream { DisposeFailure = cleanup };
        Assert.Same(inputFailure, Assert.Throws<InvalidOperationException>(() => TabularFile.CsvWriter<ExportRow>(second, false).Write(Broken())));
        Assert.Equal("Value\n1\n", Encoding.UTF8.GetString(second.Bytes));
        Assert.Equal(0, second.SyncFlushes);
    }

    [Fact]
    public async Task ValidatesModelBeforeFactoryAndDisposesInvalidFactoryStreams()
    {
        int calls = 0;
        Assert.Throws<TabularWriteException>(() => TabularFile.CsvWriter<SetterOnlyWriteRow>(() => { calls++; return new ExportProbeStream(); }).Write());
        Assert.Equal(0, calls);
        Assert.Throws<InvalidOperationException>(() => TabularFile.CsvWriter<ExportRow>(() => null!).Write());
        var stream = new ExportProbeStream(writable: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => TabularFile.CsvWriterAsync<ExportRow>(_ => new ValueTask<Stream>(stream)).WriteAsync().AsTask());
        Assert.Equal(1, stream.AsyncDisposals);
    }

    [Fact]
    public async Task SupportsCallerOwnedCompressionWrapperWithoutClosingIt()
    {
        using var bytes = new MemoryStream();
        await using (var gzip = new GZipStream(bytes, CompressionLevel.Fastest, leaveOpen: true))
        {
            Assert.Equal(1, await TabularFile.CsvWriter<ExportRow>(gzip).WriteAsync(new ExportRow { Value = 42 }));
            Assert.True(gzip.CanWrite);
        }
        bytes.Position = 0;
        using var decoder = new GZipStream(bytes, CompressionMode.Decompress, leaveOpen: true);
        using var text = new StreamReader(decoder);
        Assert.Equal("Value\n42\n", await text.ReadToEndAsync());
    }

    private static async IAsyncEnumerable<ExportRow> AsyncRows(params int[] values)
    { await Task.Yield(); foreach (int value in values) yield return new() { Value = value }; }
}

[TabularRecord] public sealed partial class ExportRow { public int Value { get; init; } }
