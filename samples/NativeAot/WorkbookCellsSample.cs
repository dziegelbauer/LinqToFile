using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using LinqToFile;
using SampleModels;

internal static class WorkbookCellsSample
{
    internal static void Run()
    {
        foreach (bool strict in new[] { false, true })
        {
            using var stream = Create(strict);
            var row = TabularFile.Xlsx<WorkbookCells>(stream)
                .WithSharedStringMemoryBudget(0)
                .WithMaxTemporaryBytes(64 * 1024)
                .WithMaxDecompressedBytes(1024 * 1024)
                .WithMaxCellTextLength(128)
                .Single();
            if (row.Name != "Ada Lovelace" || row.Posted != new DateTime(strict ? 1904 : 1900, 3, 1, 12, 0, 0) ||
                row.Posted.Kind != DateTimeKind.Unspecified || row.Recorded.Offset != TimeSpan.FromHours(-7) ||
                row.Recorded.Ticks % TimeSpan.TicksPerSecond != 1234568 || row.Duration != TimeSpan.FromHours(-36) ||
                row.Count != 2 || row.Date != new DateOnly(strict ? 1904 : 1900, 3, 1) || row.Clock != new TimeOnly(12, 0))
                throw new InvalidOperationException("The XLSX cell conversion result is incorrect.");
        }
        Console.WriteLine("XLSX cells: shared strings, formulas, temporal values, disk spill");
    }

    private static MemoryStream Create(bool strict)
    {
        var stream = WorkbookSample.Create(strict);
        XNamespace ns = strict ? "http://purl.oclc.org/ooxml/spreadsheetml/main" : "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string rel = strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships" : "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var workbook = Read(archive, "xl/workbook.xml");
            workbook.AddFirst(new XElement(ns + "workbookPr", new XAttribute("date1904", strict ? "1" : "0")));
            Write(archive, "xl/workbook.xml", workbook);
            var types = Read(archive, "[Content_Types].xml");
            types.Add(new XElement(types.Name.Namespace + "Override", new XAttribute("PartName", "/xl/sharedStrings.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml")));
            Write(archive, "[Content_Types].xml", types);
            var relationships = Read(archive, "xl/_rels/workbook.xml.rels");
            relationships.Add(new XElement(relationships.Name.Namespace + "Relationship", new XAttribute("Id", "strings"),
                new XAttribute("Type", rel + "/sharedStrings"), new XAttribute("Target", "sharedStrings.xml")));
            Write(archive, "xl/_rels/workbook.xml.rels", relationships);
            string[] headers = ["Name", "Posted", "Recorded", "Duration", "Count", "Date", "Clock"];
            var sst = new XElement(ns + "sst", headers.Select(name => new XElement(ns + "si", new XElement(ns + "t", name))));
            sst.Add(new XElement(ns + "si", new XElement(ns + "r", new XElement(ns + "t", "Ada ")),
                new XElement(ns + "r", new XElement(ns + "t", "_x004C_ovelace"))));
            Write(archive, "xl/sharedStrings.xml", sst);
            XElement Cell(string reference, string type, string value) => new(ns + "c", new XAttribute("r", reference),
                new XAttribute("t", type), new XElement(ns + "v", value));
            var header = new XElement(ns + "row", new XAttribute("r", "1"), headers.Select((_, i) =>
                Cell($"{(char)('A' + i)}1", "s", i.ToString(CultureInfo.InvariantCulture))));
            var formula = new XElement(ns + "c", new XAttribute("r", "E2"), new XElement(ns + "f", "SUM(1,1)"), new XElement(ns + "v", "2"));
            var data = new XElement(ns + "row", new XAttribute("r", "2"), Cell("A2", "s", "7"),
                Cell("B2", "n", strict ? "60.5" : "61.5"), Cell("C2", "d", "2026-11-01T01:30:45.12345675-07:00"),
                Cell("D2", "n", "-1.5"), formula, Cell("F2", "n", strict ? "60" : "61"), Cell("G2", "n", "0.5"));
            Write(archive, "xl/worksheets/data.xml", new XElement(ns + "worksheet", new XElement(ns + "sheetData", header, data)));
        }
        stream.Position = 0;
        return stream;
    }

    private static XElement Read(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        return XElement.Load(stream);
    }
    private static void Write(ZipArchive archive, string name, XElement xml)
    {
        archive.GetEntry(name)?.Delete();
        using var stream = archive.CreateEntry(name).Open();
        xml.Save(stream);
    }
}
