using LinqToFile;
using SampleModels;

internal static class ConstructorMappingSample
{
    internal static async Task RunAsync()
    {
        using var csv = new MemoryStream("product_id,Name\n7,Widget\n"u8.ToArray());
        var product = TabularFile.Csv<ImmutableProduct>(csv).Single();
        if (product.Id != 7 || product.Name != "Widget")
            throw new InvalidOperationException("Constructor mapping failed.");

        using var workbook = new MemoryStream();
        await TabularFile.XlsxWriter<ImmutableProduct>(workbook).WriteAsync(product);
        workbook.Position = 0;
        var result = (await TabularFile.Xlsx<ImmutableProduct>(workbook).ToArrayAsync()).Single();
        if (result.Id != product.Id || result.Name != product.Name)
            throw new InvalidOperationException("XLSX constructor mapping failed.");
        Console.WriteLine("Constructor mapping: CSV and XLSX passed.");
    }
}
