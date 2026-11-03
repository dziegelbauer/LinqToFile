using System.Globalization;
using LinqToFile;
using SampleModels;

internal static class XlsxWritingSample
{
    internal static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LinqToFile-XlsxExport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var outputPath = Path.Combine(directory, "people.xlsx");
            var writer = TabularFile.XlsxWriter<Person>(outputPath)
                .WithWorksheet("People");
            long written = await writer.WriteAsync(new[]
            {
                new Person { Id = 1, Name = "Ada", Age = 36 },
                new Person { Id = 2, Name = "Grace", Age = 17 }
            });
            var people = await TabularFile.Xlsx<Person>(outputPath)
                .WithWorksheet("People")
                .ToArrayAsync();
            if (written != 2 || people.Length != 2 || people[0].Name != "Ada")
                throw new InvalidOperationException("XLSX path export failed.");

            using var stream = new MemoryStream();
            await TabularFile.XlsxWriter<Person>(stream)
                .WithWorksheet("People")
                .WriteAsync(people);
            stream.Position = 0;
            var copied = await TabularFile.Xlsx<Person>(stream).ToArrayAsync();
            if (copied.Length != 2 || copied[1].Name != "Grace") throw new InvalidOperationException("XLSX stream export failed.");

            var replacement = writer.WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
            if (replacement.Write(people[0]) != 1 || await replacement.WriteAsync(people) != 2)
                throw new InvalidOperationException("XLSX replacement failed.");

            var scalar = new ScalarRecord
            {
                Name = " literal =SUM(A1:A2) _x0041_\0雪😀 ", Boolean = true, Character = '雪',
                Decimal = 12.5m, Double = 1.25, Int64 = 123456789012345, UInt128 = 42,
                Date = new DateOnly(2026, 11, 3), Timestamp = new DateTime(2026, 11, 3, 12, 0, 0), Time = new TimeOnly(12, 0),
                TimestampOffset = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromMinutes(-345)).AddTicks(1234567),
                Duration = TimeSpan.MinValue, State = SampleStatus.Ready, Access = SamplePermission.Read | SamplePermission.Write
            };
            var scalarPath = Path.Combine(directory, "scalars.xlsx");
            await TabularFile.XlsxWriter<ScalarRecord>(scalarPath).WithDateSystem(TabularDateSystem.Excel1904)
                .WithCulture(CultureInfo.GetCultureInfo("fr-FR")).WriteAsync(scalar);
            var result = TabularFile.Xlsx<ScalarRecord>(scalarPath).WithCulture(CultureInfo.GetCultureInfo("fr-FR")).Single();
            if (result.Name != scalar.Name || result.Timestamp != scalar.Timestamp || result.Time != scalar.Time ||
                result.Date != scalar.Date || result.TimestampOffset != scalar.TimestampOffset ||
                result.TimestampOffset.Offset != scalar.TimestampOffset.Offset || result.Duration != scalar.Duration ||
                result.Decimal != scalar.Decimal || result.Double != scalar.Double || result.UInt128 != scalar.UInt128 ||
                result.Int64 != scalar.Int64 || result.Character != scalar.Character || result.Access != scalar.Access)
                throw new InvalidOperationException("XLSX native scalar round trip failed.");

            var converterPath = Path.Combine(directory, "converters.xlsx");
            TabularFile.XlsxWriter<Product>(converterPath).Write(new Product { Code = new ProductCode(42), Name = "=literal" });
            if (TabularFile.Xlsx<Product>(converterPath).Single().Code.Value != 42)
                throw new InvalidOperationException("XLSX converter output failed.");

            var sink = new StreamWritingSample.AsyncOnlySink();
            var asyncWriter = TabularFile.XlsxWriterAsync<InventoryItem>(async token =>
            { await Task.Yield(); token.ThrowIfCancellationRequested(); return sink; });
            if (await asyncWriter.WriteAsync(Items()) != 2 || !sink.Disposed)
                throw new InvalidOperationException("XLSX async-only factory failed.");
            using var payload = new MemoryStream(sink.Bytes);
            var items = TabularFile.Xlsx<InventoryItem>(payload).ToArray();
            if (items.Length != 2 || items[0].Available != true || items[1].Available is not null || items[1].Note != "")
                throw new InvalidOperationException("XLSX annotation output failed.");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("XLSX writing: native cells, text/converters/tokens, paths/overwrite, streams, async factories, and both date systems passed.");
    }

    private static async IAsyncEnumerable<InventoryItem> Items()
    {
        yield return new InventoryItem { Id = 1, Available = true, ExpiresOn = new DateOnly(1800, 12, 31) };
        await Task.Yield();
        yield return new InventoryItem { Id = 2, Note = "" };
    }
}
