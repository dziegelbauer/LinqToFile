using LinqToFile;
using SampleModels;

internal static class XlsxMutationSample
{
    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Mutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "people.xlsx");
            await TabularFile.XlsxWriter<Person>(path).WithWorksheet("People").WriteAsync(new[]
            {
                new Person { Id = 1, Name = "Ada", Age = 36 },
                new Person { Id = 3, Name = "Katherine", Age = 40 }
            });

            var file = TabularFile.Xlsx<Person>(path).WithWorksheet("People");
            await file.InsertAsync(1, new Person { Id = 2, Name = "Grace", Age = 37 });
            await file.AppendRangeAsync(new[]
            {
                new Person { Id = 4, Name = "Mary", Age = 30 }
            });
            var people = await file.ToArrayAsync();
            if (!people.Select(person => person.Id).SequenceEqual(new[] { 1, 2, 3, 4 }))
                throw new InvalidOperationException("XLSX append/insertion example failed.");

            string csvPath = Path.Combine(directory, "incoming.csv");
            await TabularFile.Csv<Person>(csvPath).CreateAsync(new[]
            {
                new Person { Id = 5, Name = "Dorothy" }, new Person { Id = 6, Name = "Annie" }
            });
            var cancellationToken = CancellationToken.None;
            var incoming = TabularFile.Csv<Person>(csvPath).AsAsyncEnumerable();
            long appended = await file.AppendRangeAsync(incoming, cancellationToken);
            if (appended != 2 || file.Count() != 6) throw new InvalidOperationException("XLSX async input failed.");

            if (file.Append(new Person { Id = 7, Name = "Joan" }) != 1 ||
                file.AppendRange(new[] { new Person { Id = 8, Name = "Jean" } }) != 1 ||
                file.Insert(0, new Person { Id = 0, Name = "First" }) != 1 ||
                file.InsertRange(1, new[] { new Person { Id = 9, Name = "Second" } }) != 1 ||
                await file.InsertRangeAsync(2, new[] { new Person { Id = 10, Name = "Third" } }) != 1 ||
                await file.InsertRangeAsync(3, OnePerson()) != 1 ||
                await file.AppendAsync(new Person { Id = 12, Name = "Last" }) != 1 || file.Count() != 13)
                throw new InvalidOperationException("XLSX mutation overloads failed.");
            if (file.AppendRange(Array.Empty<Person>()) != 0) throw new InvalidOperationException("XLSX empty range failed.");

            string productsPath = Path.Combine(directory, "products.xlsx");
            TabularFile.XlsxWriter<Product>(productsPath).Write(new Product { Code = new ProductCode(1), Name = "old" });
            await TabularFile.Xlsx<Product>(productsPath).AppendAsync(new Product { Code = new ProductCode(2), Name = "=literal _x0041_" });
            if (TabularFile.Xlsx<Product>(productsPath).Last().Code.Value != 2)
                throw new InvalidOperationException("XLSX mutation converter failed.");

            string tokensPath = Path.Combine(directory, "tokens.xlsx");
            TabularFile.XlsxWriter<InventoryItem>(tokensPath).WithDateSystem(TabularDateSystem.Excel1904)
                .Write(new InventoryItem { Id = 1, Available = true, ExpiresOn = new DateOnly(1800, 12, 31) });
            var tokens = TabularFile.Xlsx<InventoryItem>(tokensPath);
            await tokens.InsertAsync(0, new InventoryItem { Id = 2, Available = false, Note = "", ExpiresOn = new DateOnly(1800, 12, 30) });
            if (tokens.First().Available != false || tokens.First().ExpiresOn != new DateOnly(1800, 12, 30))
                throw new InvalidOperationException("XLSX mutation annotation failed.");

            string nativePath = Path.Combine(directory, "native.xlsx");
            var native = new ScalarRecord
            {
                Name = " native 雪😀 ", Date = new DateOnly(2026, 11, 3), Time = new TimeOnly(12, 0),
                Timestamp = new DateTime(2026, 11, 3, 12, 0, 0), State = SampleStatus.Ready,
                Access = SamplePermission.Read, Decimal = 12.5m, Character = '雪'
            };
            TabularFile.XlsxWriter<ScalarRecord>(nativePath).WithDateSystem(TabularDateSystem.Excel1904).Write(native);
            var nativeFile = TabularFile.Xlsx<ScalarRecord>(nativePath);
            await nativeFile.AppendAsync(native);
            var result = nativeFile.Last();
            if (nativeFile.Count() != 2 || result.Date != native.Date || result.Time != native.Time || result.Timestamp != native.Timestamp || result.Decimal != 12.5m)
                throw new InvalidOperationException("XLSX native mutation failed.");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("XLSX mutation: append/insertion overloads, async ranges, converters, annotations, and native date-system preservation passed.");
    }
    private static async IAsyncEnumerable<Person> OnePerson()
    { await Task.Yield(); yield return new Person { Id = 11, Name = "Fourth" }; }
}
