using LinqToFile;
using SampleModels;

internal static class TokenSample
{
    internal static async Task RunAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        string workbookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var file = TabularFile.Csv<InventoryItem>(path)
                .WithDelimiter(';')
                .WithQuote('\'')
                .WithFieldTrimming();
            await file.CreateAsync(new[]
            {
                new InventoryItem { Id = 1, Available = true, ExpiresOn = new DateOnly(2026, 12, 31) },
                new InventoryItem { Id = 2, Available = false, Note = "" }
            });
            var rows = await file.ToListAsync();
            if (rows.Count != 2 || rows[0].Available != true || rows[0].Note is not null ||
                rows[0].ExpiresOn != new DateOnly(2026, 12, 31) || rows[1].Available != false || rows[1].Note != "" || rows[1].ExpiresOn is not null)
                throw new InvalidOperationException("Boolean/null token round trip failed.");
            await file.AppendRangeAsync(MoreRows());
            file.Insert(1, new InventoryItem { Id = 4, Available = true, Note = "nUlL" });
            var updated = file.ToArray();
            if (!updated.Select(r => r.Id).SequenceEqual(new[] { 1, 4, 2, 3 }) || updated[1].Note != "nUlL" ||
                updated[3].Available is not null || updated[3].Note != "fresh; '雪'")
                throw new InvalidOperationException("Token append/insert or CSV escaping failed.");

            byte[] before = await File.ReadAllBytesAsync(path);
            bool rejected = false;
            try { await file.AppendAsync(new InventoryItem { Id = 5, Note = " NULL " }); }
            catch (TabularWriteException error) when (error is { TargetMember: "Note", InnerException: null }) { rejected = true; }
            byte[] after = await File.ReadAllBytesAsync(path);
            if (!rejected || !before.AsSpan().SequenceEqual(after))
                throw new InvalidOperationException("A non-null value collided with a null token.");

            QuickStartSample.CreateWorkbook(workbookPath);
            var native = await TabularFile.Xlsx<NativeTokenNumber>(workbookPath).ToListAsync();
            if (!native.Select(r => r.Id).SequenceEqual(new int?[] { 1, 2 }))
                throw new InvalidOperationException("A null token consumed a native numeric cell.");
        }
        finally { File.Delete(path); File.Delete(workbookPath); }
        Console.WriteLine("Column tokens: boolean/null values, empty strings, temporal formats, async ranges, collisions, and native XLSX passed.");
    }

    private static async IAsyncEnumerable<InventoryItem> MoreRows()
    {
        await Task.Yield();
        yield return new InventoryItem { Id = 3, Note = "fresh; '雪'" };
    }
}
