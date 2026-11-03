using System.Text;
using Xunit;
using LinqToFile.Tests.Support;

namespace LinqToFile.Tests;

public sealed partial class StreamWriterTests
{
    [Fact]
    public void RejectsInvalidConfigurationWithoutConsumingTheDestination()
    {
        Assert.Throws<ArgumentNullException>(() => TabularFile.CsvWriter<ExportRow>((Stream)null!));
        using var readOnly = new MemoryStream([], writable: false);
        Assert.Throws<ArgumentException>(() => TabularFile.CsvWriter<ExportRow>(readOnly));
        Assert.Throws<ArgumentNullException>(() => TabularFile.CsvWriter<ExportRow>((Func<Stream>)null!));
        Assert.Throws<ArgumentNullException>(() => TabularFile.CsvWriterAsync<ExportRow>(null!));
        using var stream = new MemoryStream();
        var writer = TabularFile.CsvWriter<ExportRow>(stream);
        Assert.Throws<ArgumentException>(() => writer.WithDelimiter('"'));
        Assert.Throws<ArgumentException>(() => writer.WithQuote(','));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithDelimiter('\0'));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithMaxFieldLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithMaxRecordLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WithNewLine((LineEnding)99));
        Assert.Throws<ArgumentException>(() => writer.WithHeaderComparison(StringComparer.CurrentCulture));
        Assert.Throws<ArgumentNullException>(() => writer.WithEncoding(null!));
        Assert.Throws<ArgumentNullException>(() => writer.WithCulture(null!));
        Assert.Equal(0, writer.Write());
    }

    [Fact]
    public async Task LargeRecordCancellationCanLeavePartialBytesAndStopsFurtherIo()
    {
        using var cancellation = new CancellationTokenSource();
        ExportProbeStream? stream = null;
        stream = new ExportProbeStream(asyncOnly: true)
        {
            AfterWrite = () => { if (stream!.AsyncWrites == 2) cancellation.Cancel(); }
        };
        using (stream)
        {
            string text = new('雪', 20000);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TabularFile.CsvWriter<TokenText>(stream)
                .WriteAsync(new TokenText { Value = text }, cancellation.Token).AsTask());
            Assert.Equal(2, stream.AsyncWrites);
            Assert.InRange(stream.Bytes.Length, 7, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(0, stream.AsyncFlushes);
        }
    }

    [Fact]
    public async Task EncoderFailuresKeepPrivateRowDiagnosticsAndOwnedCleanup()
    {
        var stream = new ExportProbeStream(asyncOnly: true);
        var error = await Assert.ThrowsAsync<TabularWriteException>(() => TabularFile.CsvWriter<TokenText>(stream, false)
            .WriteAsync(new TokenText { Value = "private-\uD800" }).AsTask());
        Assert.Equal(1, error.RecordNumber);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private-", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.AsyncFlushes);
    }

    [Fact]
    public async Task StreamsSupportSingleByteEncodingsAndEncodingSnapshots()
    {
        using var stream = new MemoryStream();
        var encoding = (Encoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = new EncoderReplacementFallback("?");
        var writer = TabularFile.CsvWriter<TokenText>(stream).WithEncoding(encoding);
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        await writer.WriteAsync(new TokenText { Value = "雪" });
        Assert.Equal("Value\n?\n", Encoding.ASCII.GetString(stream.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumericExportsDoNotAddAllocationsAsTheRowCountGrows(bool asynchronous)
    {
        await Scan(20000, asynchronous);
        long small = long.MaxValue, large = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            small = Math.Min(small, await Scan(1000, asynchronous));
            large = Math.Min(large, await Scan(100000, asynchronous));
        }
        Assert.InRange(large - small, -4096, 4096);
    }

    private static async ValueTask<long> Scan(int count, bool asynchronous)
    {
        using var stream = new CountingOutput();
        var writer = TabularFile.CsvWriter<ExportRow>(stream);
        var rows = Enumerable.Repeat(new ExportRow { Value = 42 }, count);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long result = asynchronous ? await writer.WriteAsync(rows) : writer.Write(rows);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(count, result);
        Assert.Equal(6L + 3L * count, stream.Bytes);
        return bytes;
    }

    private sealed class CountingOutput : Stream
    {
        internal long Bytes { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Bytes += count;
        public override void Write(ReadOnlySpan<byte> buffer) => Bytes += buffer.Length;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Bytes += buffer.Length; return default; }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
