using System.IO.Compression;
using System.Text;
using LinqToFile;
using SampleModels;

internal static class ConverterSample
{
    internal static async Task RunAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var products = TabularFile.Csv<Product>(path);
            await products.CreateAsync(new[] { new Product { Code = new ProductCode(42), Name = "Tea" } });
            await products.AppendAsync(new Product { Code = new ProductCode(7), Name = "Coffee" });
            await products.InsertAsync(1, new Product { Code = new ProductCode(9), Name = "Milk" });
            var selected = await products.AsAsyncEnumerable().Where(p => p.Code.Value > 8).ToListAsync();
            if (!selected.Select(p => p.Code.Value).SequenceEqual(new[] { 42, 9 }) || products.Count() != 3)
                throw new InvalidOperationException("CSV converter round trip failed.");
            if (!File.ReadAllText(path).Contains("P-42", StringComparison.Ordinal))
                throw new InvalidOperationException("The custom formatter was not used.");

            using var workbook = CreateWorkbook();
            var native = await TabularFile.Xlsx<Product>(workbook).ToListAsync();
            if (native.Count != 2 || native[0].Code.Value != 42 || native[1].Code.Value != 7)
                throw new InvalidOperationException("XLSX native/text converter mapping failed.");

            File.Delete(path);
            var stock = TabularFile.Csv<ConvertedStock>(path);
            await stock.CreateAsync(new[] { new ConvertedStock { Available = false }, new ConvertedStock { Available = true, Count = 5 } });
            var counts = await stock.ToArrayAsync();
            if (counts.Length != 2 || counts[0].Available || counts[0].Count is not null || !counts[1].Available || counts[1].Count != 5)
                throw new InvalidOperationException("Nullable/class converter round trip failed.");
        }
        finally { File.Delete(path); }
        Console.WriteLine("Custom converters passed: CSV writes, sync/async queries, and native XLSX cells.");
    }

    private static MemoryStream CreateWorkbook()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Add("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="office" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Products" sheetId="1" r:id="sheet"/></sheets></workbook>
                """);
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="sheet" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            Add("xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
                <row r="1"><c r="A1" t="inlineStr"><is><t>product_code</t></is></c><c r="B1" t="inlineStr"><is><t>Name</t></is></c></row>
                <row r="2"><c r="A2"><v>42</v></c><c r="B2" t="inlineStr"><is><t>Tea</t></is></c></row>
                <row r="3"><c r="A3" t="inlineStr"><is><t>P-7</t></is></c><c r="B3" t="inlineStr"><is><t>Coffee</t></is></c></row>
                </sheetData></worksheet>
                """);
            void Add(string name, string xml)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
        }
        stream.Position = 0;
        return stream;
    }
}
