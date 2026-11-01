using System.Text;
using System.Runtime.CompilerServices;
using Xunit;

namespace LinqToFile.Tests;

public sealed class StreamingAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumericStreamingAllocatesOnlyOwnedModelsAsRecordCountGrows(bool asynchronous)
    {
        // Every read completes inline: no unrelated thread allocations enter this counter.
        await Scan(1000, asynchronous);
        long small = await Scan(10_000, asynchronous);
        long large = await Scan(1_000_000, asynchronous);
        long models = ModelBytes(990_000);
        Assert.InRange(large - small - models, -4096, 4096);
    }

    private static async ValueTask<long> Scan(int count, bool asynchronous)
    {
        var source = TabularFile.Csv<AllocationRow>(() => new RepeatedStream(count));
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
    private sealed class RepeatedStream(int records) : Stream
    {
        private static readonly byte[] Header = Encoding.UTF8.GetBytes("Value\n");
        private static readonly byte[] Row = Encoding.UTF8.GetBytes("42\n");
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
            long end = Header.Length + (long)records * Row.Length;
            while (written < buffer.Length && position < end)
            {
                ReadOnlySpan<byte> source = position < Header.Length
                    ? Header.AsSpan((int)position)
                    : Row.AsSpan((int)((position - Header.Length) % Row.Length));
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
    public int Value { get; init; }
}
