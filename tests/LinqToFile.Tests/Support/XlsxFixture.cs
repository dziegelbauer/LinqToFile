using System.IO.Compression;
using System.Security;
using System.Text;

namespace LinqToFile.Tests.Support;

// Test fixtures are deliberately built from XML/package literals, independently of reader helpers.
internal static class XlsxFixture
{
    internal static MemoryStream Create(string rows, bool strict = false, string afterData = "",
        Action<Dictionary<string, string>>? change = null)
    {
        string ns = strict ? "http://purl.oclc.org/ooxml/spreadsheetml/main" : "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string rel = strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships" : "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/data.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>",
            ["_rels/.rels"] = $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"office\" Type=\"{rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>",
            ["xl/workbook.xml"] = $"<workbook xmlns=\"{ns}\" xmlns:r=\"{rel}\"><sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"sheet\" state=\"hidden\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"sheet\" Type=\"{rel}/worksheet\" Target=\"worksheets/data.xml\"/></Relationships>",
            ["xl/worksheets/data.xml"] = $"<worksheet xmlns=\"{ns}\"><sheetData>{rows}</sheetData>{afterData}</worksheet>"
        };
        change?.Invoke(parts);
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, xml) in parts)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }
        stream.Position = 0;
        return stream;
    }

    internal static string Text(string reference, string value) =>
        $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(value)}</t></is></c>";
    internal static string Number(string reference, string value) => $"<c r=\"{reference}\"><v>{SecurityElement.Escape(value)}</v></c>";
    internal static string Cell(string reference, string type, string value) =>
        $"<c r=\"{reference}\" t=\"{type}\"><v>{SecurityElement.Escape(value)}</v></c>";
    internal static string Row(int row, params string[] cells) => $"<row r=\"{row}\">{string.Concat(cells)}</row>";
    internal static string SingleColumn(string value, string type = "n") =>
        Row(1, Text("A1", "Value")) + Row(2, Cell("A2", type, value));
}
