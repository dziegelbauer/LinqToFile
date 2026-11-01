using System.Diagnostics;
using LinqToFile;

var path = Path.GetTempFileName();
try
{
    using (var writer = new StreamWriter(path))
    {
        writer.WriteLine("Value");
        for (int i = 0; i < 100_000; i++) writer.WriteLine(i);
    }
    var source = TabularFile.Csv<NumericRow>(path);
    long Scan()
    {
        long sum = 0;
        foreach (var row in source) sum += row.Value;
        return sum;
    }
    for (int i = 0; i < 5; i++) Scan();
    var timings = new List<double>();
    var allocations = new List<long>();
    for (int i = 0; i < 5; i++)
    {
        var start = Stopwatch.GetTimestamp();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = Scan();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (sum != 4_999_950_000L) throw new InvalidOperationException("Incorrect benchmark result.");
        timings.Add(elapsed);
        allocations.Add(allocated);
    }
    timings.Sort();
    allocations.Sort();
    Console.WriteLine($"100000 numeric records; median of 5 runs after 5 warmups");
    Console.WriteLine($"Elapsed: {timings[2]:F2} ms; allocated: {allocations[2]} bytes ({allocations[2] / 100000.0:F2} bytes/record)");
    Console.WriteLine("Includes one owned object per record, parser buffers, file opening, and schema binding; excludes input creation.");
}
finally { File.Delete(path); }

[TabularRecord]
public sealed partial class NumericRow
{
    public int Value { get; init; }
}
