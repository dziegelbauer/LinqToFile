using System.Globalization;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxWriterBoundaryTests
{
    [Fact]
    public void RowLimitIncludesTheHeaderAndValidationPrecedesFactoryInvocation()
    {
        var output = new XlsxOutput<ExportRow>(new XlsxWriteOptions());
        output.Row(new ExportRow { Value = 1 }, 1_048_575);
        var error = Assert.Throws<TabularWriteException>(() => output.Row(new ExportRow(), 1_048_576));
        Assert.Equal(1_048_577, error.WorksheetRowNumber);
        int calls = 0;
        var writer = TabularFile.XlsxWriter<WriteText>(() => { calls++; return new MemoryStream(); }).WithMaxCellTextLength(0);
        Assert.Throws<TabularSchemaException>(() => writer.Write());
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ActiveRowFormattingDoesNotAllocatePerRow()
    {
        var output = new XlsxOutput<XlsxExportRow>(new XlsxWriteOptions { Culture = CultureInfo.GetCultureInfo("fr-FR") });
        var row = new XlsxExportRow
        {
            Amount = 12.5m, Number = 42, Text = " literal _x0041_ <&> 雪😀 ", Enabled = false,
            Date = new(2026, 11, 3), Timestamp = new(2026, 11, 3, 12, 0, 0), Time = new(12, 0),
            Offset = new(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(-7)), Duration = TimeSpan.FromDays(-2)
        };
        Scan(output, row, 20_000);
        long allocated = long.MaxValue;
        for (int i = 0; i < 3; i++) allocated = Math.Min(allocated, Scan(output, row, 100_000));
        Assert.InRange(allocated, 0, 512);
    }

    private static long Scan(XlsxOutput<XlsxExportRow> output, XlsxExportRow row, int count)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i <= count; i++) output.Row(row, i);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public async Task InputFailureSurvivesCancellationDuringArchiveCleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("input failure");
        async IAsyncEnumerable<ExportRow> Rows()
        {
            yield return new() { Value = 1 };
            await Task.Yield(); cancellation.Cancel(); throw failure;
        }
        var stream = new ExportProbeStream(asyncOnly: true);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TabularFile.XlsxWriter<ExportRow>(stream, false).WriteAsync(Rows(), cancellation.Token).AsTask()));
        Assert.Equal(1, stream.AsyncDisposals);
    }

    [Fact]
    public async Task SuccessfulOwnedDisposalIsAwaitedAndFinalFlushReceivesCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ExportProbeStream(asyncOnly: true) { BeforeAsyncDispose = async () => { entered.SetResult(); await release.Task; } };
        var pending = TabularFile.XlsxWriter<ExportRow>(stream, false).WriteAsync().AsTask();
        await entered.Task;
        Assert.False(pending.IsCompleted); release.SetResult();
        Assert.Equal(0, await pending);
        using var cancellation = new CancellationTokenSource();
        var flush = new ExportProbeStream(asyncOnly: true)
        {
            BeforeAsyncFlush = token => { Assert.Equal(cancellation.Token, token); cancellation.Cancel(); return Task.CompletedTask; }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TabularFile.XlsxWriter<ExportRow>(flush, false).WriteAsync(cancellation.Token).AsTask());
        Assert.Equal(1, flush.AsyncDisposals);
    }
}
