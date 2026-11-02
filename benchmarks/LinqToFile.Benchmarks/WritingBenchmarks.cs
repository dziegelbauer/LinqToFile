using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LinqToFile;
using LinqToFile.Formats.Csv;
using LinqToFile.Writing;

internal static class WritingBenchmarks
{
    private const int BlockSize = 64 * 1024;
    internal static async Task RunAsync(string[] args)
    {
        var switches = new HashSet<string>(["--rows", "--existing-rows", "--iterations", "--buffer", "--only", "--output"], StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--writing") continue;
            if (!switches.Contains(args[i]) || i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Unknown option or missing value: " + args[i]);
            i++;
        }
        int rows = Number("--rows", 10_000), existing = Number("--existing-rows", 100_000);
        int iterations = Number("--iterations", 5), buffer = Number("--buffer", 4096);
        string? filter = Option("--only"), output = Option("--output");
        string root = Path.Combine(Path.GetTempPath(), "LinqToFile-WriteBench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var measurements = new List<object>();
        var heaps = new List<object>();
        try
        {
            await Matrix("numeric", new NumericRow { Value = 7 }, new NumericRow { Value = 42 },
                rows, existing, "7\n", "42\n");
            await Matrix("quoted", new TextRow { Value = "old,\"雪\"\r\nline" }, new TextRow { Value = "new,\"😀雪\"\r\nline" },
                Math.Min(rows, 10_000), Math.Min(existing, 50_000), "\"old,\"\"雪\"\"\r\nline\"\n", "\"new,\"\"😀雪\"\"\r\nline\"\n");
            await Matrix("large", new TextRow { Value = new string('x', 4096) }, new TextRow { Value = new string('y', 4096) },
                Math.Min(rows, 200), Math.Min(existing, 1000), new string('x', 4096) + "\n", new string('y', 4096) + "\n");

            var report = new
            {
                SchemaVersion = 1, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Rows = rows, ExistingRows = existing, Iterations = iterations, BufferSize = buffer,
                WarmupMinimumScans = 5, WarmupMinimumWorkSeconds = 1, InitialWarmupSeconds = 3,
                AllocationCounter = "GC.GetTotalAllocatedBytes(precise: true); entire process; reused input model",
                ResetAndByteVerificationExcluded = true, MovementBlockBytes = BlockSize,
                Results = measurements, LiveHeapAndTemporaryFiles = heaps
            };
            if (output is not null)
            {
                string path = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("Report: " + path);
            }
        }
        finally { Directory.Delete(root, true); }

        async Task Matrix<T>(string name, T oldRow, T newRow, int count, int oldCount, string oldText, string newText)
            where T : class, ITabularRecord<T>
        {
            string template = Path.Combine(root, name + "-template.csv"), target = Path.Combine(root, name + "-target.csv");
            TabularFile.Csv<T>(template).Create(Rows(oldRow, oldCount));
            long oldLength = new FileInfo(template).Length;
            byte[] header = Encoding.UTF8.GetBytes("Value\n"), oldBytes = Encoding.UTF8.GetBytes(oldText), newBytes = Encoding.UTF8.GetBytes(newText);
            int middle = oldCount / 2;
            var options = new CsvOptions { BufferSize = buffer };
            var formatter = new CsvOutput<T>(options);
            formatter.Row(newRow, 1);
            int recordCharacters = formatter.Record.Length;
            if (name == "numeric")
            {
                long warmup = Stopwatch.GetTimestamp();
                do
                {
                    for (int i = 0; i < count; i++) formatter.Row(newRow, i + 1);
                } while (Stopwatch.GetElapsedTime(warmup).TotalSeconds < 3);
            }
            await Measure(name + "-enumeration-sync", count, oldCount, oldLength, 0, NoReset,
                () => new(Rows(newRow, count).LongCount()), count, NoVerify);
            await Measure(name + "-enumeration-async", count, oldCount, oldLength, 0, NoReset, async () =>
            {
                long total = 0; await foreach (var _ in AsyncRows(newRow, count)) total++; return total;
            }, count, NoVerify);
            await Measure(name + "-format-only", count, oldCount, oldLength, (long)newBytes.Length * count, NoReset, () =>
            {
                long sum = 0;
                for (int i = 0; i < count; i++) { formatter.Row(newRow, i + 1); sum += formatter.Record.Length; }
                return new(sum);
            }, (long)recordCharacters * count, NoVerify);

            foreach (bool asynchronous in new[] { false, true })
            {
                string execution = asynchronous ? "async" : "sync";
                await Measure(name + "-validate-" + execution, oldCount, oldCount, oldLength, 0, NoReset, async () =>
                {
                    await using var stream = Open(template, FileAccess.Read, asynchronous);
                    if (asynchronous) await CsvWritePreparation<T>.ReadAsync(stream, options, default);
                    else CsvWritePreparation<T>.Read(stream, options);
                    return oldCount;
                }, oldCount, NoVerify);
                await Measure(name + "-locate-" + execution, oldCount, oldCount, oldLength, 0, NoReset, async () =>
                {
                    await using var stream = Open(template, FileAccess.Read, asynchronous);
                    var plan = asynchronous ? await CsvInsertionPreparation<T>.ReadAsync(stream, options, middle, default)
                        : CsvInsertionPreparation<T>.Read(stream, options, middle);
                    return plan.Offset;
                }, header.Length + (long)oldBytes.Length * middle, NoVerify);
                await Measure(name + "-copy-" + execution, oldCount, oldCount, oldLength, 0, DeleteTarget, async () =>
                {
                    await using var input = Open(template, FileAccess.Read, asynchronous);
                    await using var destination = Open(target, FileAccess.Write, asynchronous, FileMode.CreateNew);
                    if (asynchronous) await input.CopyToAsync(destination); else input.CopyTo(destination);
                    return destination.Position;
                }, oldLength, () => Verify(0, 0, oldCount));
                await Measure(name + "-rotate-only-" + execution, count, oldCount, oldLength, (long)newBytes.Length * count,
                    () => { Reset(); using var stream = new FileStream(target, FileMode.Append); for (int i = 0; i < count; i++) stream.Write(newBytes); },
                    async () =>
                    {
                        await using var stream = Open(target, FileAccess.ReadWrite, asynchronous);
                        byte[] left = new byte[BlockSize], right = new byte[BlockSize];
                        if (asynchronous) await StreamSegments.RotateAsync(stream, header.Length + (long)middle * oldBytes.Length, oldLength, left, right);
                        else StreamSegments.Rotate(stream, header.Length + (long)middle * oldBytes.Length, oldLength, left, right);
                        return count;
                    }, count, () => Verify(middle, count, oldCount - middle));

                foreach (bool direct in new[] { false, true })
                {
                    string mode = direct ? "direct" : "staged";
                    var file = TabularFile.Csv<T>(target).WithBufferSize(buffer).WithInPlaceWrites(direct);
                    await Measure(name + "-create-" + mode + "-" + execution, count, 0, 0, (long)newBytes.Length * count,
                        DeleteTarget, () => asynchronous ? file.CreateAsync(AsyncRows(newRow, count)) : new(file.Create(Rows(newRow, count))),
                        count, () => Verify(0, count, 0));
                    await Measure(name + "-append-" + mode + "-" + execution, count, oldCount, oldLength, (long)newBytes.Length * count,
                        Reset, () => asynchronous ? file.AppendRangeAsync(AsyncRows(newRow, count)) : new(file.AppendRange(Rows(newRow, count))),
                        count, () => Verify(oldCount, count, 0));
                    var quick = file.WithAppendValidation(false);
                    await Measure(name + "-quick-append-" + mode + "-" + execution, count, oldCount, oldLength, (long)newBytes.Length * count,
                        Reset, () => asynchronous ? quick.AppendRangeAsync(AsyncRows(newRow, count)) : new(quick.AppendRange(Rows(newRow, count))),
                        count, () => Verify(oldCount, count, 0));
                    await Measure(name + "-insert-" + mode + "-" + execution, count, oldCount, oldLength, (long)newBytes.Length * count,
                        Reset, () => asynchronous ? file.InsertRangeAsync(middle, AsyncRows(newRow, count)) : new(file.InsertRange(middle, Rows(newRow, count))),
                        count, () => Verify(middle, count, oldCount - middle));
                }
                int shortCount = Math.Min(count, 100);
                var shortFile = TabularFile.Csv<T>(target).WithInPlaceWrites().WithAppendValidation(false);
                await Measure(name + "-short-single-" + execution, shortCount, oldCount, oldLength, (long)newBytes.Length * shortCount,
                    Reset, async () =>
                    {
                        long written = 0;
                        for (int i = 0; i < shortCount; i++)
                            written += asynchronous ? await shortFile.AppendAsync(newRow) : shortFile.Append(newRow);
                        return written;
                    }, shortCount, () => Verify(oldCount, shortCount, 0));
                await Measure(name + "-short-range-" + execution, shortCount, oldCount, oldLength, (long)newBytes.Length * shortCount,
                    Reset, () => asynchronous ? shortFile.AppendRangeAsync(AsyncRows(newRow, shortCount)) : new(shortFile.AppendRange(Rows(newRow, shortCount))),
                    shortCount, () => Verify(oldCount, shortCount, 0));
            }
            if (filter is null)
            {
                foreach (bool direct in new[] { false, true })
                foreach (string operation in new[] { "create", "append", "insert" })
                {
                    if (operation == "create") DeleteTarget(); else Reset();
                    long baseline = GC.GetTotalMemory(true);
                    var points = new HashSet<int> { 1, Math.Max(2, count / 10), count };
                    IEnumerable<T> ProbeRows()
                    {
                        for (int i = 1; i <= count; i++)
                        {
                            yield return newRow;
                            if (points.Contains(i))
                            {
                                long live = GC.GetTotalMemory(true);
                                heaps.Add(new
                                {
                                    Name = name + "-" + operation, Direct = direct, RowsSupplied = i,
                                    LiveManagedBytes = live,
                                    LiveManagedBytesAboveBaseline = live - baseline,
                                    TemporaryLogicalBytes = Directory.EnumerateFiles(root, ".linqtofile-*.tmp").Sum(path => new FileInfo(path).Length)
                                });
                            }
                        }
                    }
                    var file = TabularFile.Csv<T>(target).WithInPlaceWrites(direct);
                    long actual = operation switch
                    {
                        "create" => await file.CreateAsync(Forward(ProbeRows())),
                        "append" => await file.AppendRangeAsync(Forward(ProbeRows())),
                        _ => await file.InsertRangeAsync(middle, Forward(ProbeRows()))
                    };
                    if (actual != count || Directory.EnumerateFiles(root, ".linqtofile-*.tmp").Any()) throw new InvalidOperationException("Memory probe failed.");
                }
            }

            void Reset() => File.Copy(template, target, overwrite: true);
            void DeleteTarget() => File.Delete(target);
            void Verify(int before, int added, int after)
            {
                using var stream = File.OpenRead(target);
                byte[] scratch = new byte[Math.Max(header.Length, Math.Max(oldBytes.Length, newBytes.Length))];
                Match(header, 1); Match(oldBytes, before); Match(newBytes, added); Match(oldBytes, after);
                if (stream.ReadByte() != -1) throw new InvalidOperationException("Unexpected trailing bytes.");
                void Match(byte[] expected, int repeat)
                {
                    for (int i = 0; i < repeat; i++)
                    {
                        stream.ReadExactly(scratch.AsSpan(0, expected.Length));
                        if (!scratch.AsSpan(0, expected.Length).SequenceEqual(expected)) throw new InvalidOperationException("Byte verification failed.");
                    }
                }
            }
        }

        async Task Measure(string name, int count, int oldCount, long oldBytes, long newBytes, Action reset,
            Func<ValueTask<long>> action, long expected, Action verify)
        {
            if (filter is not null && !name.Contains(filter, StringComparison.Ordinal)) return;
            int warmups = 0; double workSeconds = 0;
            do
            {
                reset(); long start = Stopwatch.GetTimestamp(); long actual = await action();
                workSeconds += Stopwatch.GetElapsedTime(start).TotalSeconds;
                if (actual != expected) throw new InvalidOperationException("Warmup result failed: " + name);
                verify(); warmups++;
            } while (warmups < 5 || workSeconds < 1);
            var samples = new List<object>(); var times = new List<double>(); var allocations = new List<long>();
            for (int i = 0; i < iterations; i++)
            {
                reset();
                long before = GC.GetTotalAllocatedBytes(true), start = Stopwatch.GetTimestamp();
                long actual = await action();
                double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                long bytes = GC.GetTotalAllocatedBytes(true) - before;
                if (actual != expected) throw new InvalidOperationException("Measured result failed: " + name);
                verify();
                samples.Add(new { Milliseconds = milliseconds, AllocatedBytes = bytes, Result = actual });
                times.Add(milliseconds); allocations.Add(bytes);
            }
            double median = times.Order().ElementAt(iterations / 2);
            long allocated = allocations.Order().ElementAt(iterations / 2);
            measurements.Add(new { Name = name, NewOrScannedRows = count, ExistingRows = oldCount, ExistingBytes = oldBytes, NewBytes = newBytes,
                ExpectedPeakTemporaryResultBytes = name.Contains("-staged-", StringComparison.Ordinal)
                    ? (name.Contains("-create-", StringComparison.Ordinal) ? Encoding.UTF8.GetByteCount("Value\n") : oldBytes) + newBytes : 0,
                Warmups = warmups, MedianMilliseconds = median, MedianAllocatedBytes = allocated, Samples = samples });
            Console.WriteLine($"{name}: {median:F2} ms; {allocated} B/operation; {allocated / (double)count:F2} B/row");
        }
        string? Option(string key) { int index = Array.IndexOf(args, key); return index >= 0 ? args[index + 1] : null; }
        int Number(string key, int fallback) => Option(key) is string text && int.TryParse(text, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : Option(key) is null ? fallback : throw new ArgumentException(key);
    }

    private static void NoReset() { }
    private static void NoVerify() { }
    private static FileStream Open(string path, FileAccess access, bool asynchronous, FileMode mode = FileMode.Open) =>
        new(path, mode, access, FileShare.None, 4096, asynchronous ? FileOptions.Asynchronous : FileOptions.None);
    private static IEnumerable<T> Rows<T>(T row, int count) { for (int i = 0; i < count; i++) yield return row; }
    private static IAsyncEnumerable<T> AsyncRows<T>(T row, int count) => Forward(Rows(row, count));
    private static async IAsyncEnumerable<T> Forward<T>(IEnumerable<T> rows)
    {
        int index = 0;
        foreach (var row in rows) { if ((index++ & 1023) == 0) await Task.Yield(); yield return row; }
    }
}
