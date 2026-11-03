using LinqToFile;
using SampleModels;

internal static class HeaderlessCsvSample
{
    internal static async Task RunAsync()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "1,unused,Ada\n2,unused,Grace\n");
            var entries = TabularFile.Csv<HeaderlessEntry>(path)
                .WithColumnIndexes()
                .ToList();
            var namedEntries = await TabularFile.Csv<HeaderlessEntry>(path)
                .WithColumnNames("id", "unused", "name")
                .ToArrayAsync();
            if (entries.Count != 2 || entries[0].Id != 1 || entries[1].Name != "Grace" ||
                namedEntries.Length != 2 || namedEntries[0].Name != "Ada")
                throw new InvalidOperationException("Headerless CSV mapping failed.");
            var source = TabularFile.CsvAsync<HeaderlessEntry>(_ =>
                ValueTask.FromResult<Stream>(new MemoryStream("3,unused,Lin"u8.ToArray())))
                .WithColumnIndexes().WithBufferSize(1);
            if ((await source.ToListAsync()).Single().Name != "Lin")
                throw new InvalidOperationException("Async headerless CSV mapping failed.");
            Console.WriteLine("Headerless CSV: supplied names and generated indexes passed");
        }
        finally { File.Delete(path); }
    }
}
