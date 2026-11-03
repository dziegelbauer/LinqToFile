using System.Text;
using System.Runtime.CompilerServices;
using Xunit;

namespace LinqToFile.Tests;

public sealed class StreamingAllocationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task NumericStreamingAllocatesOnlyOwnedModelsAsRecordCountGrows(bool asynchronous, bool headerless, bool indexes)
    {
        // Every read completes inline: no unrelated thread allocations enter this counter.
        // Warm both parsing and buffer refills; 1,000 short rows fit in the initial buffer.
        await Scan(100_000, asynchronous, headerless, indexes);
        ModelBytes(1000);
        // One-time runtime work can occur on the measured thread. The minimum of repeated
        // scans excludes that noise; a per-record allocation regression remains in every scan.
        long small = long.MaxValue, large = long.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            small = Math.Min(small, await Scan(10_000, asynchronous, headerless, indexes));
            large = Math.Min(large, await Scan(1_000_000, asynchronous, headerless, indexes));
        }
        long models = ModelBytes(990_000);
        Assert.InRange(large - small - models, -4096, 4096);
    }

    private static async ValueTask<long> Scan(int count, bool asynchronous, bool headerless, bool indexes)
    {
        var source = TabularFile.Csv<AllocationRow>(() => new RepeatedStream(count, headerless));
        if (headerless) source = indexes ? source.WithColumnIndexes() : source.WithColumnNames("Value");
        long before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        if (asynchronous)
        {
            await foreach (var row in source) sum += row.Value;
        }
        else
        {
            foreach (var row in source) sum += row.Value;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal((long)count * 42, sum);
        return allocated;
    }

    private static long ModelBytes(int count)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < count; i++) Observe(new AllocationRow { Value = 42 });
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Force reference escape so JIT allocation elimination cannot change the baseline.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Observe(object value) => GC.KeepAlive(value);

    // Generate a virtual file using fixed storage, without seeking or buffering all records.
    private sealed class RepeatedStream(int records, bool headerless) : Stream
    {
        private static readonly byte[] Header = Encoding.UTF8.GetBytes("Value\n");
        private static readonly byte[] Row = Encoding.UTF8.GetBytes("42\n");
        private readonly int headerLength = headerless ? 0 : Header.Length;
        private long position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int written = 0;
            long end = headerLength + (long)records * Row.Length;
            while (written < buffer.Length && position < end)
            {
                ReadOnlySpan<byte> source = position < headerLength
                    ? Header.AsSpan((int)position)
                    : Row.AsSpan((int)((position - headerLength) % Row.Length));
                int length = Math.Min(source.Length, buffer.Length - written);
                source[..length].CopyTo(buffer[written..]);
                position += length;
                written += length;
            }
            return written;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

[TabularRecord]
public sealed partial class AllocationRow
{
    [ColumnIndex(0)] public int Value { get; init; }
}
