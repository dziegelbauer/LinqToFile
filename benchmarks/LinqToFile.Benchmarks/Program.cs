using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using LinqToFile;
using LinqToFile.Formats.Csv;

// Run this process alone. Process-wide counters include allocations on async continuations.
if (args.Contains("--writing", StringComparer.Ordinal))
{
    await WritingBenchmarks.RunAsync(args);
    return;
}
if (args.Contains("--xlsx", StringComparer.Ordinal))
{
    await WorkbookBenchmarks.RunAsync(args);
    return;
}
int rows = ReadInt("--rows", 100_000);
int iterations = ReadInt("--iterations", 7);
int bufferSize = ReadInt("--buffer", 4096);
string? output = ReadOption("--output");
var directory = Path.Combine(Path.GetTempPath(), "LinqToFile-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var results = new List<Measurement>();
var heap = new List<HeapSample>();
try
{
    var numericPath = WriteInput("numeric.csv", "Value", rows, i => i.ToString(CultureInfo.InvariantCulture));
    int textRows = Math.Min(rows, 20_000);
    const string quoted = "\"Ada,\"\"雪\"\"\r\nLovelace\"";
    int textLength = "Ada,\"雪\"\r\nLovelace".Length;
    var textPath = WriteInput("quoted.csv", "Value", textRows, _ => quoted);
    int largeRows = Math.Min(rows, 1_000);
    string large = new('x', 4096);
    var largePath = WriteInput("large.csv", "Value", largeRows, _ => large);
    var numeric = TabularFile.Csv<NumericRow>(numericPath).WithBufferSize(bufferSize);
    var text = TabularFile.Csv<TextRow>(textPath).WithBufferSize(bufferSize);
    var longText = TabularFile.Csv<TextRow>(largePath).WithBufferSize(bufferSize);
    long numericSum = (long)rows * (rows - 1) / 2;

    await Measure("numeric-file-sync", rows, new FileInfo(numericPath).Length, numericSum,
        () => ValueTask.FromResult(numeric.Sum(row => (long)row.Value)));
    await Measure("numeric-file-async", rows, new FileInfo(numericPath).Length, numericSum, async () =>
    {
        long sum = 0;
        await foreach (var row in numeric) sum += row.Value;
        return sum;
    });
    await Measure("numeric-list-async", rows, new FileInfo(numericPath).Length, numericSum, async () =>
        (await numeric.ToListAsync()).Sum(row => (long)row.Value));
    await Measure("numeric-dictionary-async", rows, new FileInfo(numericPath).Length, numericSum, async () =>
        (await numeric.ToDictionaryAsync(row => row.Value)).Values.Sum(row => (long)row.Value));
    await Measure("quoted-file-sync", textRows, new FileInfo(textPath).Length, (long)textRows * textLength,
        () => ValueTask.FromResult(text.Sum(row => (long)row.Value.Length)));
    await Measure("quoted-file-async", textRows, new FileInfo(textPath).Length, (long)textRows * textLength, async () =>
    {
        long sum = 0;
        await foreach (var row in text) sum += row.Value.Length;
        return sum;
    });
    await Measure("large-field-sync", largeRows, new FileInfo(largePath).Length, (long)largeRows * large.Length,
        () => ValueTask.FromResult(longText.Sum(row => (long)row.Value.Length)));

    // Input decoding and file I/O are excluded from the parser-only measurement.
    string numericText = File.ReadAllText(numericPath);
    await Measure("parser-only", rows, new FileInfo(numericPath).Length, rows, () =>
    {
        using var reader = new StringReader(numericText);
        var parser = new CsvParser(reader, new CsvOptions { BufferSize = bufferSize });
        parser.Read();
        long count = 0;
        while (parser.Read()) count++;
        return ValueTask.FromResult(count);
    });

    using var mappingInput = new StringReader("42\n");
    var mappingParser = new CsvParser(mappingInput, new CsvOptions());
    mappingParser.Read();
    int[] ordinals = [0];
    var mapper = GetMapper<NumericRow>();
    await Measure("mapping-only", rows, 0, (long)rows * 42, () =>
    {
        long sum = 0;
        for (int i = 0; i < rows; i++) sum += mapper.Read(mappingParser.Record, ordinals).Value;
        return ValueTask.FromResult(sum);
    });

    // Keep one enumeration alive. Collect at increasing counts without retaining its records.
    using (var enumerator = numeric.GetEnumerator())
    {
        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
            if (count == Math.Max(1, rows / 100) || count == Math.Max(1, rows / 10) || count == rows)
                heap.Add(new(count, GC.GetTotalMemory(forceFullCollection: true)));
        }
        if (count != rows) throw new InvalidOperationException("Incorrect memory probe count.");
    }
    var report = new
    {
        SchemaVersion = 1,
        Runtime = RuntimeInformation.FrameworkDescription,
        OS = RuntimeInformation.OSDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        ProcessorCount = Environment.ProcessorCount,
        Stopwatch.Frequency,
        BufferSize = bufferSize,
        WarmupMinimumScans = 5,
        WarmupMinimumSeconds = 1,
        Iterations = iterations,
        AllocationCounter = "GC.GetTotalAllocatedBytes(precise: true); entire process",
        Results = results,
        StreamingHeap = heap
    };
    if (output is not null)
    {
        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report: {fullPath}");
    }
    Console.WriteLine("Streaming live managed bytes: " + string.Join(", ", heap.Select(sample => $"{sample.Records}: {sample.LiveBytes}")));
}
finally { Directory.Delete(directory, recursive: true); }

async Task Measure(string name, int count, long bytes, long expected, Func<ValueTask<long>> scan)
{
    long warmupStart = Stopwatch.GetTimestamp();
    int warmups = 0;
    do
    {
        Check(await scan());
        warmups++;
    } while (warmups < 5 || Stopwatch.GetElapsedTime(warmupStart).TotalSeconds < 1);
    var samples = new List<Sample>(iterations);
    for (int i = 0; i < iterations; i++)
    {
        long before = GC.GetTotalAllocatedBytes(precise: true);
        long start = Stopwatch.GetTimestamp();
        long checksum = await scan();
        double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Check(checksum);
        samples.Add(new(milliseconds, allocated, checksum));
    }
    double median = samples.Select(sample => sample.Milliseconds).Order().ElementAt(iterations / 2);
    long allocationMedian = samples.Select(sample => sample.AllocatedBytes).Order().ElementAt(iterations / 2);
    results.Add(new(name, count, bytes, median, allocationMedian, samples));
    Console.WriteLine($"{name}: {median:F2} ms; {allocationMedian / (double)count:F2} bytes/record; {count / median * 1000:F0} records/s");
    return;
    void Check(long actual)
    {
        if (actual != expected) throw new InvalidOperationException($"Incorrect result for {name}: {actual}; expected {expected}.");
    }
}

static LinqToFile.Infrastructure.RecordMapper<T> GetMapper<T>() where T : class, ITabularRecord<T> => T.Mapper;

string WriteInput(string name, string header, int count, Func<int, string> record)
{
    string path = Path.Combine(directory, name);
    using var writer = new StreamWriter(path);
    writer.NewLine = "\n";
    writer.WriteLine(header);
    for (int i = 0; i < count; i++) writer.WriteLine(record(i));
    return path;
}

string? ReadOption(string name)
{
    int index = Array.IndexOf(args, name);
    if (index < 0) return null;
    if (index + 1 == args.Length) throw new ArgumentException($"Missing value for {name}.");
    return args[index + 1];
}

int ReadInt(string name, int fallback)
{
    string? value = ReadOption(name);
    if (value is null) return fallback;
    if (!int.TryParse(value, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
        throw new ArgumentException($"{name} must be a positive integer.");
    return parsed;
}

internal sealed record Sample(double Milliseconds, long AllocatedBytes, long Checksum);
internal sealed record Measurement(string Name, int Records, long InputBytes, double MedianMilliseconds,
    long MedianAllocatedBytes, List<Sample> Samples);
internal sealed record HeapSample(int Records, long LiveBytes);

[TabularRecord]
public sealed partial class NumericRow
{
    public int Value { get; init; }
}

[TabularRecord]
public sealed partial class TextRow
{
    public string Value { get; init; } = "";
}
