using System.IO.Compression;
using System.Text;
using LinqToFile;
using SampleModels;

internal static class QuickStartSample
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "LinqToFile-QuickStart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "people.csv"), outputPath = Path.Combine(root, "new.csv"),
                workbookPath = Path.Combine(root, "people.xlsx");
            File.WriteAllText(path, "person_id,full_name,Age,Balance\n1,Ada,36,12.50\n2,Grace,17,\n");
            CreateWorkbook(workbookPath);

            var adults = TabularFile.Csv<Person>(path)
                .Where(person => person.Age >= 18)
                .ToList();
            var peopleById = await TabularFile.Csv<Person>(path)
                .ToDictionaryAsync(person => person.Id);

            var file = TabularFile.Csv<Person>(outputPath);
            await file.CreateAsync(new[]
            {
                new Person { Id = 1, Name = "Ada", Age = 36 }
            });
            await file.AppendAsync(new Person { Id = 2, Name = "Grace", Age = 17 });
            await file.InsertAsync(1, new Person { Id = 3, Name = "Katherine", Age = 40 });

            var workbookAdults = TabularFile.Xlsx<Person>(workbookPath)
                .WithWorksheet("People")
                .Where(person => person.Age >= 18)
                .ToList();

            if (adults.Count != 1 || adults[0].Name != "Ada" || peopleById.Count != 2 ||
                !file.Select(person => person.Id).SequenceEqual(new[] { 1, 3, 2 }) ||
                workbookAdults.Count != 1 || workbookAdults[0].Name != "Ada")
                throw new InvalidOperationException("Quick-start examples failed.");
            Console.WriteLine("Quick start: CSV query, async dictionary, create/append/insert, and XLSX query passed.");
        }
        finally { Directory.Delete(root, true); }
    }

    internal static void CreateWorkbook(string path)
    {
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/data.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Write("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"book\" Type=\"{rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Write("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"{rel}\"><sheets><sheet name=\"People\" sheetId=\"1\" r:id=\"sheet\"/></sheets></workbook>");
        Write("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"sheet\" Type=\"{rel}/worksheet\" Target=\"data.xml\"/></Relationships>");
        Write("xl/data.xml", $"<worksheet xmlns=\"{ns}\"><sheetData>" +
            "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>person_id</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>full_name</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>Age</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Balance</t></is></c></row>" +
            "<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"B2\" t=\"inlineStr\"><is><t>Ada</t></is></c><c r=\"C2\"><v>36</v></c><c r=\"D2\"><v>12.50</v></c></row>" +
            "<row r=\"3\"><c r=\"A3\"><v>2</v></c><c r=\"B3\" t=\"inlineStr\"><is><t>Grace</t></is></c><c r=\"C3\"><v>17</v></c><c r=\"D3\"/></row>" +
            "</sheetData></worksheet>");
        void Write(string name, string value)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
            writer.Write(value);
        }
    }
}
