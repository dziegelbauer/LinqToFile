using System.IO.Compression;
using System.Text;
using LinqToFile;
using SampleModels;

internal static class WorkbookSample
{
    internal static void Run()
    {
        foreach (bool strict in new[] { false, true })
        {
            using var stream = Create(strict);
            var records = TabularFile.Xlsx<WorkbookEntry>(stream)
                .WithWorksheet("Data")
                .Where(record => record.Active)
                .ToArray();
            if (records.Length != 1 || records[0].Id != 42 || records[0].Name != "Ada" || records[0].Amount != 12.5m || !stream.CanRead)
                throw new InvalidOperationException("The XLSX sample result is incorrect.");
        }
        Console.WriteLine("XLSX: Transitional and Strict; typed records: Ada");
        WorkbookCellsSample.Run();
    }

    // This fixture uses inline text so the stage 1 sample needs no shared-string table.
    internal static MemoryStream Create(bool strict)
    {
        string ns = strict ? "http://purl.oclc.org/ooxml/spreadsheetml/main" : "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string rel = strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships" : "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/data.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Write(archive, "_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"book\" Type=\"{rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Write(archive, "xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"{rel}\"><sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"data\"/></sheets></workbook>");
            Write(archive, "xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"data\" Type=\"{rel}/worksheet\" Target=\"worksheets/data.xml\"/></Relationships>");
            Write(archive, "xl/worksheets/data.xml", $"<worksheet xmlns=\"{ns}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>entry_id</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>Active</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Amount</t></is></c></row><row r=\"2\"><c r=\"A2\"><v>42.0</v></c><c r=\"B2\" t=\"inlineStr\"><is><t>Ada</t></is></c><c r=\"C2\" t=\"b\"><v>1</v></c><c r=\"D2\"><v>12.5</v></c></row></sheetData></worksheet>");
        }
        stream.Position = 0;
        return stream;
    }

    private static void Write(ZipArchive archive, string name, string xml)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }
}
