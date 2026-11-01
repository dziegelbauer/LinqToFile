using LinqToFile.Infrastructure;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxAllocationTests
{
    [Theory]
    [InlineData("42")]
    [InlineData("42.0")]
    [InlineData("4.2e1")]
    public void NativeIntegerMappingDoesNotAllocateTemporaryFieldStrings(string stored)
    {
        var row = new TabularRow(128, null);
        row.Append(stored);
        row.EndField(CellKind.Number);
        int[] columns = [0];
        var mapper = Mapper<SheetInteger>();
        for (int i = 0; i < 10_000; i++) GC.KeepAlive(mapper.Read(row, columns));
        // Keep the allocation bound strict, while excluding one-time runtime work on this thread.
        // A temporary field string would allocate on every pass and still fail the minimum.
        long bytes = long.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            int sum = 0;
            for (int i = 0; i < 10_000; i++) sum += mapper.Read(row, columns).Value;
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(420_000, sum);
        }
        Assert.InRange(bytes, 0, 24L * 10_000 + 1024);
    }

    private static RecordMapper<T> Mapper<T>() where T : class, ITabularRecord<T> => T.Mapper;
}
