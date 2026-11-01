using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CancellationTests
{
    [Theory]
    [InlineData(false, "array")]
    [InlineData(true, "array")]
    [InlineData(false, "list")]
    [InlineData(true, "list")]
    [InlineData(false, "dictionary")]
    [InlineData(true, "dictionary")]
    public async Task PreCancelledFirstMoveClaimsSuppliedStreamAndRespectsOwnership(bool leaveOpen, string materializer)
    {
        var stream = new ProbeStream("Value\n1"u8.ToArray(), asyncOnly: true);
        var source = TabularFile.Csv<NumberRow>(stream, leaveOpen);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Task operation = materializer switch
        {
            "list" => source.ToListAsync(cancellation.Token).AsTask(),
            "dictionary" => source.ToDictionaryAsync(r => r.Value, cancellationToken: cancellation.Token).AsTask(),
            _ => source.ToArrayAsync(cancellation.Token).AsTask()
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(0, stream.SyncReads + stream.AsyncReads);
        Assert.Equal(leaveOpen ? 0 : 1, stream.AsyncDisposals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.WithBufferSize(1).ToArrayAsync().AsTask());
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CancellationBetweenBufferedRowsDoesNotRequireAnotherIoOperation()
    {
        var stream = new ProbeStream("Value\n1\n2\n3"u8.ToArray(), asyncOnly: true);
        using var cancellation = new CancellationTokenSource();
        await using var reader = TabularFile.Csv<NumberRow>(stream, false).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await reader.MoveNextAsync());
        int reads = stream.AsyncReads;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        Assert.Equal(reads, stream.AsyncReads);
        Assert.Equal(1, stream.AsyncDisposals);
    }

    [Fact]
    public async Task CancellationInterruptsPendingAsyncIoAndDisposesOwnedStream()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ProbeStream("Value\n1"u8.ToArray(), asyncOnly: true)
        {
            BeforeReadAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };
        using var cancellation = new CancellationTokenSource();
        var operation = TabularFile.Csv<NumberRow>(stream, false).ToListAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.SyncReads);
    }

    [Fact]
    public async Task AsyncFactoryReceivesEnumeratorCancellationToken()
    {
        CancellationToken received = default;
        ProbeStream? opened = null;
        using var cancellation = new CancellationTokenSource();
        var source = TabularFile.CsvAsync<NumberRow>(token =>
        {
            received = token;
            opened = new ProbeStream("Value\n1"u8.ToArray(), asyncOnly: true);
            return new ValueTask<Stream>(opened);
        });
        await foreach (var row in source.AsAsyncEnumerable().WithCancellation(cancellation.Token))
            Assert.Equal(1, row.Value);
        Assert.Equal(cancellation.Token, received);
        Assert.Equal(1, opened!.AsyncDisposals);
    }

    [Fact]
    public async Task CancellationDuringFactoryOpeningDisposesReturnedStream()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new ProbeStream("Value\n1"u8.ToArray(), asyncOnly: true);
        var source = TabularFile.CsvAsync<NumberRow>(_ =>
        {
            cancellation.Cancel();
            return new ValueTask<Stream>(stream);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ToArrayAsync(cancellation.Token).AsTask());
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.AsyncReads);
    }

    [Fact]
    public async Task PreCancellationDoesNotInvokeFactories()
    {
        int calls = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var syncFactory = TabularFile.Csv<NumberRow>(() =>
        {
            calls++;
            return new MemoryStream("Value\n1"u8.ToArray());
        });
        var asyncFactory = TabularFile.CsvAsync<NumberRow>(_ =>
        {
            calls++;
            return new ValueTask<Stream>(new MemoryStream("Value\n1"u8.ToArray()));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => syncFactory.ToListAsync(cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asyncFactory.ToListAsync(cancellation.Token).AsTask());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task IoErrorsKeepTheirTypeAndStillDisposeOwnedStreams()
    {
        var stream = new ProbeStream("Value\n1"u8.ToArray(), asyncOnly: true)
        {
            BeforeReadAsync = _ => ValueTask.FromException(new IOException("Synthetic read failure."))
        };
        await Assert.ThrowsAsync<IOException>(() => TabularFile.Csv<NumberRow>(stream, false).ToArrayAsync().AsTask());
        Assert.Equal(1, stream.AsyncDisposals);
    }
}
