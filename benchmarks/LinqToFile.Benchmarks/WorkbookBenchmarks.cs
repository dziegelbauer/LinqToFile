using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LinqToFile;

internal static class WorkbookBenchmarks
{
    internal static async Task RunAsync(string[] args)
    {
        int rows = Number("--rows", 50_000);
        int iterations = Number("--iterations", 5);
        if (rows > 1_048_575) throw new ArgumentOutOfRangeException(nameof(rows));
        string root = Path.Combine(Path.GetTempPath(), "LinqToFile-XlsxBench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var results = new List<object>();
        var heaps = new List<object>();
        try
        {
            string numeric = Create("numeric", rows);
            string inline = Create("inline", rows);
            string shared = Create("shared", rows);
            string unique = Create("unique", rows);
            int largeRows = Math.Min(rows, 500);
            string large = Create("large", largeRows);
            long expected = (long)rows * (rows - 1) / 2;
            var numbers = TabularFile.Xlsx<NumericRow>(numeric);
            // Allow shared XML/conversion methods to tier before comparing source shapes.
            long tiering = Stopwatch.GetTimestamp();
            do
            {
                if (numbers.Sum(r => (long)r.Value) != expected) throw new InvalidOperationException("Warmup checksum failed.");
                long sum = 0;
                await foreach (var row in numbers) sum += row.Value;
                if (sum != expected) throw new InvalidOperationException("Async warmup checksum failed.");
            } while (Stopwatch.GetElapsedTime(tiering).TotalSeconds < 3);
            await Measure("numeric-sync", rows, numeric, expected, () => new(numbers.Sum(r => (long)r.Value)));
            await Measure("numeric-async", rows, numeric, expected, async () =>
            {
                long total = 0;
                await foreach (var row in numbers) total += row.Value;
                return total;
            });
            var forward = TabularFile.Xlsx<NumericRow>(() => new ForwardOnly(new FileStream(numeric,
                FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous)))
                .WithTemporaryDirectory(root);
            await Measure("numeric-nonseekable-sync", rows, numeric, expected, () => new(forward.Sum(r => (long)r.Value)));
            await Measure("numeric-nonseekable-async", rows, numeric, expected, async () =>
            {
                long total = 0;
                await foreach (var row in forward) total += row.Value;
                return total;
            });
            foreach (var (name, path, count, budget, checksum) in new[]
            {
                ("inline", inline, rows, 8L * 1024 * 1024, (long)rows * 32),
                ("shared-cache", shared, rows, 8L * 1024 * 1024, (long)rows * 32),
                ("shared-spill", shared, rows, 0L, (long)rows * 32),
                ("unique-cache", unique, rows, 64L * 1024 * 1024, (long)rows * 32),
                ("unique-small-budget", unique, rows, 1024L, (long)rows * 32),
                ("large-cell", large, largeRows, 8L * 1024 * 1024, (long)largeRows * 16384)
            })
            {
                var source = TabularFile.Xlsx<TextRow>(path).WithSharedStringMemoryBudget(budget).WithTemporaryDirectory(root);
                await Measure(name + "-sync", count, path, checksum, () => new(source.Sum(r => (long)r.Value.Length)));
                await Measure(name + "-async", count, path, checksum, async () =>
                {
                    long total = 0;
                    await foreach (var row in source) total += row.Value.Length;
                    return total;
                });
            }
            await Measure("preflight-first-record", 1, numeric, 0, () => new((long)numbers.First().Value));
            await Measure("unique-spill-first-record", 1, unique, 32, () => new((long)TabularFile.Xlsx<TextRow>(unique)
                .WithSharedStringMemoryBudget(0).First().Value.Length));

            foreach (var (name, path, budget) in new[] { ("inline", inline, 0L), ("unique-cache", unique, 64L * 1024 * 1024), ("unique-spill", unique, 0L) })
            {
                long baseline = GC.GetTotalMemory(true);
                var source = TabularFile.Xlsx<TextRow>(path).WithSharedStringMemoryBudget(budget).WithTemporaryDirectory(root);
                using var enumerator = source.GetEnumerator();
                long start = Stopwatch.GetTimestamp();
                int count = 0;
                while (enumerator.MoveNext())
                {
                    count++;
                    if (count == 1 || count == Math.Max(2, rows / 10) || count == rows)
                        heaps.Add(new { Name = name, Records = count, LiveManagedBytesAboveBaseline = GC.GetTotalMemory(true) - baseline,
                            TemporaryLogicalBytes = Directory.EnumerateFiles(root, "*.tmp").Sum(p => new FileInfo(p).Length),
                            ElapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds });
                }
                if (count != rows) throw new InvalidOperationException("Wrong heap probe count.");
            }
            var report = new { SchemaVersion = 1, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Rows = rows, Iterations = iterations, WarmupMinimumScans = 5, WarmupMinimumSeconds = 1,
                AllocationCounter = "GC.GetTotalAllocatedBytes(precise: true); entire process",
                InitialNumericWarmupSeconds = 3,
                Results = results, StreamingHeap = heaps };
            string? output = Option("--output");
            if (output is not null)
            {
                string path = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Report: {path}");
            }
        }
        finally { Directory.Delete(root, true); }

        async Task Measure(string name, int count, string path, long expected, Func<ValueTask<long>> scan)
        {
            int warmups = 0;
            long start = Stopwatch.GetTimestamp();
            do { Check(await scan()); warmups++; } while (warmups < 5 || Stopwatch.GetElapsedTime(start).TotalSeconds < 1);
            var samples = new List<object>();
            var times = new List<double>();
            var allocations = new List<long>();
            for (int i = 0; i < iterations; i++)
            {
                long before = GC.GetTotalAllocatedBytes(true);
                start = Stopwatch.GetTimestamp();
                long checksum = await scan();
                double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                long bytes = GC.GetTotalAllocatedBytes(true) - before;
                Check(checksum);
                times.Add(milliseconds); allocations.Add(bytes);
                samples.Add(new { Milliseconds = milliseconds, AllocatedBytes = bytes, Checksum = checksum });
            }
            double median = times.Order().ElementAt(iterations / 2);
            long allocated = allocations.Order().ElementAt(iterations / 2);
            results.Add(new { Name = name, Records = count, InputBytes = new FileInfo(path).Length,
                MedianMilliseconds = median, MedianAllocatedBytes = allocated, Samples = samples });
            Console.WriteLine($"{name}: {median:F2} ms; {allocated / (double)count:F2} bytes/record");
            void Check(long actual) { if (actual != expected) throw new InvalidOperationException($"Wrong checksum for {name}."); }
        }

        string Create(string kind, int count)
        {
            string path = Path.Combine(root, kind + ".xlsx");
            using var stream = File.Create(path);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            bool shared = kind is "shared" or "unique";
            bool unique = kind == "unique";
            Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/data.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" + (shared ? "<Override PartName=\"/xl/strings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" : "") + "</Types>");
            Write("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"book\" Type=\"{rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Write("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"{rel}\"><sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"sheet\"/></sheets></workbook>");
            Write("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"sheet\" Type=\"{rel}/worksheet\" Target=\"data.xml\"/>" + (shared ? $"<Relationship Id=\"strings\" Type=\"{rel}/sharedStrings\" Target=\"strings.xml\"/>" : "") + "</Relationships>");
            if (shared)
            {
                using var writer = Writer("xl/strings.xml");
                writer.Write($"<sst xmlns=\"{ns}\">");
                for (int i = 0; i < (unique ? count : 1); i++) writer.Write($"<si><t>{i.ToString("D32", CultureInfo.InvariantCulture)}</t></si>");
                writer.Write("</sst>");
            }
            using (var writer = Writer("xl/data.xml"))
            {
                writer.Write($"<worksheet xmlns=\"{ns}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Value</t></is></c></row>");
                string large = new('x', 16384);
                for (int i = 0; i < count; i++)
                {
                    string coordinate = "A" + (i + 2).ToString(CultureInfo.InvariantCulture);
                    writer.Write($"<row r=\"{i + 2}\"><c r=\"{coordinate}\"");
                    if (kind == "numeric") writer.Write($"><v>{i}</v>");
                    else if (shared) writer.Write($" t=\"s\"><v>{(unique ? i : 0)}</v>");
                    else writer.Write($" t=\"inlineStr\"><is><t>{(kind == "large" ? large : i.ToString("D32", CultureInfo.InvariantCulture))}</t></is>");
                    writer.Write("</c></row>");
                }
                writer.Write("</sheetData></worksheet>");
            }
            return path;
            StreamWriter Writer(string name) => new(zip.CreateEntry(name, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
            void Write(string name, string xml) { using var writer = Writer(name); writer.Write(xml); }
        }
        string? Option(string key) { int i = Array.IndexOf(args, key); return i < 0 ? null : i + 1 < args.Length ? args[i + 1] : throw new ArgumentException(key); }
        int Number(string key, int fallback) => Option(key) is string value ? int.Parse(value, CultureInfo.InvariantCulture) is > 0 and var n ? n : throw new ArgumentException(key) : fallback;
    }

    private sealed class ForwardOnly(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer, token);
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
