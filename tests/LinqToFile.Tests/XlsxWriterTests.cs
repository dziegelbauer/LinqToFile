using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxWriterTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-XlsxWrite-" + Guid.NewGuid().ToString("N"));
    public XlsxWriterTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string Destination => Path.Combine(directory, "data.xlsx");
    private void NoTemporaryFiles() => Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));

    [Theory]
    [InlineData(false, TabularDateSystem.Excel1900)]
    [InlineData(true, TabularDateSystem.Excel1900)]
    [InlineData(false, TabularDateSystem.Excel1904)]
    [InlineData(true, TabularDateSystem.Excel1904)]
    public async Task NativeCellsRoundTripThroughBothDateSystemsAndHaveStyles(bool asynchronous, TabularDateSystem system)
    {
        var original = new XlsxExportRow
        {
            Amount = 12.5m, Enabled = true, Text = " literal <&> 雪😀 _x0041_\r\n", Number = 123456789012345,
            Date = new(2026, 11, 3), Timestamp = new(2026, 11, 3, 12, 0, 0), Time = new(12, 0),
            Offset = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromMinutes(-345)).AddTicks(1234567),
            Duration = TimeSpan.MinValue
        };
        var writer = TabularFile.XlsxWriter<XlsxExportRow>(Destination).WithWorksheet("Orders & 'items' sheet")
            .WithCulture(CultureInfo.GetCultureInfo("fr-FR")).WithDateSystem(system);
        Assert.Equal(1, asynchronous ? await writer.WriteAsync(original) : writer.Write(original));
        var reader = TabularFile.Xlsx<XlsxExportRow>(Destination).WithWorksheet("orders & 'items' sheet")
            .WithCulture(CultureInfo.GetCultureInfo("fr-FR"));
        Assert.Equivalent(original, reader.Single(), strict: true);
        Assert.Equivalent(original, Assert.Single(await reader.ToArrayAsync()), strict: true);
        using var archive = ZipFile.OpenRead(Destination);
        Assert.Null(archive.GetEntry("xl/sharedStrings.xml"));
        using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = xml.Descendants(ns + "row").Last().Elements(ns + "c").ToArray();
        Assert.Equal("12.5", cells[0].Element(ns + "v")!.Value);
        Assert.Equal("1", cells[1].Attribute("s")!.Value); // Date, sorted by header.
        Assert.Equal("b", cells[3].Attribute("t")!.Value); // Enabled.
        Assert.Equal("2", cells[^1].Attribute("s")!.Value); // Timestamp.
        NoTemporaryFiles();
    }

    [Fact]
    public async Task AllInputOverloadsHeadersOnlyAndOwnershipBehaveLikeCsv()
    {
        using var memory = new MemoryStream();
        Assert.Equal(0, TabularFile.XlsxWriter<ExportRow>(memory).Write());
        memory.Position = 0;
        Assert.Empty(TabularFile.Xlsx<ExportRow>(memory));
        var writer = TabularFile.XlsxWriter<ExportRow>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
        Assert.Equal(2, writer.Write(new[] { new ExportRow { Value = 1 }, new ExportRow { Value = 2 } }));
        Assert.Equal(2, await writer.WriteAsync(new[] { new ExportRow { Value = 3 }, new ExportRow { Value = 4 } }));
        Assert.Equal(2, await writer.WriteAsync(Rows()));
        Assert.Equal(new[] { 5, 6 }, TabularFile.Xlsx<ExportRow>(Destination).Select(row => row.Value));
        Assert.Equal(0, await writer.WriteAsync());
        Assert.Empty(TabularFile.Xlsx<ExportRow>(Destination));
        static async IAsyncEnumerable<ExportRow> Rows()
        { await Task.Yield(); yield return new() { Value = 5 }; yield return new() { Value = 6 }; }
    }

    [Fact]
    public async Task NonseekableAsyncOnlyStreamsAndFactoriesRequireNoSynchronousOutput()
    {
        var streams = new ConcurrentBag<ExportProbeStream>();
        var writer = TabularFile.XlsxWriterAsync<ExportRow>(async token =>
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            var stream = new ExportProbeStream(asyncOnly: true); streams.Add(stream); return stream;
        });
        Assert.Throws<InvalidOperationException>(() => writer.Write());
        Assert.Empty(streams);
        Assert.Equal(new long[] { 1, 1 }, await Task.WhenAll(writer.WriteAsync(new ExportRow { Value = 1 }).AsTask(),
            writer.WithWorksheet("Second").WriteAsync(new ExportRow { Value = 2 }).AsTask()));
        Assert.Equal(2, streams.Count);
        foreach (var stream in streams)
        {
            Assert.Equal(0, stream.SyncWrites); Assert.Equal(0, stream.SyncFlushes); Assert.Equal(0, stream.SyncDisposals);
            Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(1, stream.AsyncFlushes);
            using var bytes = new MemoryStream(stream.Bytes);
            Assert.InRange(TabularFile.Xlsx<ExportRow>(bytes).Single().Value, 1, 2);
        }
        using var supplied = new ExportProbeStream(asyncOnly: true);
        var once = TabularFile.XlsxWriter<ExportRow>(supplied);
        Assert.Throws<ArgumentNullException>(() => once.Write((IEnumerable<ExportRow>)null!));
        Assert.Throws<InvalidOperationException>(() => once.WithExistingFileBehavior(ExistingFileBehavior.Throw));
        Assert.Equal(0, await once.WithWorksheet("Header").WriteAsync());
        Assert.False(supplied.IsDisposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => once.WriteAsync().AsTask());
        var owned = new ExportProbeStream();
        Assert.Equal(0, TabularFile.XlsxWriter<ExportRow>(owned, false).Write());
        Assert.Equal(1, owned.SyncDisposals);
    }

    [Fact]
    public async Task SyncFactoriesRepeatAndCurrentPositionIsTheArchiveOrigin()
    {
        using var bytes = new MemoryStream();
        bytes.Write("prefix"u8);
        Assert.Equal(1, await TabularFile.XlsxWriter<ExportRow>(bytes).WriteAsync(new ExportRow { Value = 42 }));
        Assert.Equal("prefix"u8.ToArray(), bytes.ToArray()[..6]);
        bytes.Position = 6;
        Assert.Equal(42, TabularFile.Xlsx<ExportRow>(bytes).Single().Value);
        var opened = new List<ExportProbeStream>();
        var writer = TabularFile.XlsxWriter<ExportRow>(() => { var stream = new ExportProbeStream(); opened.Add(stream); return stream; });
        writer.Write(); await writer.WriteAsync();
        Assert.Equal(1, opened[0].SyncDisposals); Assert.Equal(1, opened[1].AsyncDisposals);
    }

    [Fact]
    public async Task PathOutputPreservesOriginalOnInputFailureCancellationAndPublicationRace()
    {
        File.WriteAllText(Destination, "original");
        var original = TabularFile.XlsxWriter<ExportRow>(Destination);
        Assert.Throws<IOException>(() => original.Write());
        var writer = original.WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
        IEnumerable<ExportRow> Broken() { yield return new() { Value = 1 }; throw new InvalidOperationException("input"); }
        Assert.Throws<InvalidOperationException>(() => writer.Write(Broken()));
        Assert.Equal("original", File.ReadAllText(Destination));
        using var cancellation = new CancellationTokenSource();
        async IAsyncEnumerable<ExportRow> Cancelled([EnumeratorCancellation] CancellationToken token = default)
        { yield return new() { Value = 1 }; await Task.Yield(); cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(Cancelled(), cancellation.Token).AsTask());
        Assert.Equal("original", File.ReadAllText(Destination));
        File.Delete(Destination);
        IEnumerable<ExportRow> Race() { File.WriteAllText(Destination, "competitor"); yield return new() { Value = 1 }; }
        Assert.Throws<IOException>(() => original.Write(Race()));
        Assert.Equal("competitor", File.ReadAllText(Destination));
        NoTemporaryFiles();
    }

    [Fact]
    public async Task PrecancelledAndOverlappingSuppliedAttemptsAreConsumedAndDisposedAsRequested()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var stream = new ExportProbeStream(asyncOnly: true);
        var writer = TabularFile.XlsxWriter<ExportRow>(stream, false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(cancellation.Token).AsTask());
        Assert.Empty(stream.Bytes); Assert.Equal(1, stream.AsyncDisposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync().AsTask());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var blocked = new ExportProbeStream(asyncOnly: true) { BeforeAsyncWrite = async _ => { entered.TrySetResult(); await release.Task; } };
        var once = TabularFile.XlsxWriter<ExportRow>(blocked);
        var first = once.WriteAsync().AsTask();
        await entered.Task;
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => once.WithWorksheet("Other").WriteAsync().AsTask()); }
        finally { release.SetResult(); }
        Assert.Equal(0, await first);
    }

    [Fact]
    public async Task FlushAndCleanupFailuresAreReportedAndOriginalErrorsRemainPrimary()
    {
        var flush = new IOException("flush");
        var cleanup = new IOException("cleanup");
        var stream = new ExportProbeStream(asyncOnly: true) { FlushFailure = flush, DisposeFailure = cleanup };
        Assert.Same(flush, await Assert.ThrowsAsync<IOException>(() => TabularFile.XlsxWriter<ExportRow>(stream, false).WriteAsync().AsTask()));
        Assert.Equal(1, stream.AsyncDisposals);
        var failing = new ExportProbeStream { DisposeFailure = cleanup };
        Assert.Same(cleanup, Assert.Throws<IOException>(() => TabularFile.XlsxWriter<ExportRow>(failing, false).Write()));
        using var cancellation = new CancellationTokenSource();
        var cancelled = new ExportProbeStream(asyncOnly: true) { AfterWrite = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TabularFile.XlsxWriter<ExportRow>(cancelled, false).WriteAsync(cancellation.Token).AsTask());
        Assert.NotEmpty(cancelled.Bytes); Assert.Equal(1, cancelled.AsyncDisposals);
        Assert.Equal(0, cancelled.AsyncFlushes);
    }

    [Fact]
    public void LimitsSchemaAndBlankRowsThrowPrivateLocatedErrors()
    {
        var writer = TabularFile.XlsxWriter<WriteText>(Destination);
        var blank = Assert.Throws<TabularWriteException>(() => writer.Write(new WriteText()));
        Assert.Equal(1, blank.RecordNumber); Assert.Equal(2, blank.WorksheetRowNumber); Assert.Equal("Sheet1", blank.WorksheetName);
        var huge = Assert.Throws<TabularWriteException>(() => writer.Write(new WriteText { Value = new string('s', 32768) }));
        Assert.Equal("A2", huge.CellReference); Assert.Equal("Value", huge.TargetMember); Assert.Null(huge.InnerException);
        Assert.DoesNotContain(new string('s', 100), huge.ToString(), StringComparison.Ordinal);
        Assert.Throws<TabularWriteException>(() => writer.Write(new WriteText { Value = new string('\n', 254) }));
        Assert.Throws<TabularSchemaException>(() => writer.WithMaxCellTextLength(0).Write());
        int calls = 0;
        Assert.Throws<TabularWriteException>(() => TabularFile.XlsxWriter<SetterOnlyWriteRow>(() => { calls++; return new MemoryStream(); }).Write());
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Destination)); NoTemporaryFiles();
    }

    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData(" _x0041_ _x005F_x0041_\r\n\t雪😀 ")]
    [InlineData("\0\u0001\uD800x\uDC00\uFFFE\uFFFF")]
    public async Task LiteralTextAndUtf16SpreadsheetEscapesRoundTrip(string text)
    {
        Assert.Equal(1, await TabularFile.XlsxWriter<WriteText>(Destination).WriteAsync(new WriteText { Value = text }));
        Assert.Equal(text, TabularFile.Xlsx<WriteText>(Destination).Single().Value);
        using var archive = ZipFile.OpenRead(Destination);
        using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "f");
    }

    [Fact]
    public async Task TokenAndTemporalAnnotationsSelectTextAndNativeZeroIsNotANullToken()
    {
        var zero = TabularFile.XlsxWriter<TokenNumber>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
        await zero.WriteAsync(new[] { new TokenNumber { Value = 0 }, new TokenNumber() });
        Assert.Equal(new int?[] { 0, null }, TabularFile.Xlsx<TokenNumber>(Destination).Select(row => row.Value));
        await TabularFile.XlsxWriter<TokenNullableBoolean>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite)
            .WriteAsync(new[] { new TokenNullableBoolean { Value = true }, new TokenNullableBoolean() });
        Assert.Equal(new bool?[] { true, null }, TabularFile.Xlsx<TokenNullableBoolean>(Destination).Select(row => row.Value));
        await TabularFile.XlsxWriter<TokenDate>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite)
            .WriteAsync(new TokenDate { Value = new(1800, 12, 31) });
        Assert.Equal(new DateOnly(1800, 12, 31), TabularFile.Xlsx<TokenDate>(Destination).Single().Value);
        var collision = TabularFile.XlsxWriter<TokenText>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite).WithFieldTrimming();
        Assert.Throws<TabularWriteException>(() => collision.Write(new TokenText { Value = " NULL " }));
    }

    [Fact]
    public void InvalidOptionsAreRejectedBeforeAnExportAttempt()
    {
        using var stream = new MemoryStream();
        var writer = TabularFile.XlsxWriter<ExportRow>(stream);
        foreach (string name in new[] { "", " ", "bad/name", "'bad", "bad'", new string('x', 32), "bad\0", "History", "history" })
            Assert.ThrowsAny<ArgumentException>(() => writer.WithWorksheet(name));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithDateSystem(TabularDateSystem.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithMaxCellTextLength(-1));
        Assert.Throws<ArgumentException>(() => writer.WithHeaderComparison(StringComparer.CurrentCulture));
        Assert.Throws<ArgumentNullException>(() => writer.Write((ExportRow)null!));
        Assert.Equal(0, writer.Write());
    }
}

[TabularRecord]
public sealed partial class XlsxExportRow
{
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Enabled { get; init; }
    public long Number { get; init; }
    public DateTimeOffset Offset { get; init; }
    public string Text { get; init; } = "";
    public TimeOnly Time { get; init; }
    public DateTime Timestamp { get; init; }
}
