using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Tests.Support;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxMutationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-XlsxMutate-" + Guid.NewGuid().ToString("N"));
    public XlsxMutationTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string PathFor(string name = "data.xlsx") => Path.Combine(directory, name);
    private void Save(MemoryStream fixture) => File.WriteAllBytes(PathFor(), fixture.ToArray());
    private void Clean() => Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    private static Dictionary<string, byte[]> Parts(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var input = e.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        });
    }
    private static XElement Sheet(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        using var input = zip.Entries.Single(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)).Open();
        return XElement.Load(input);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AllOperationsPreserveTheVariantAndUseLogicalIndexes(bool strict, bool asynchronous)
    {
        using var fixture = Create(SingleColumn("1"), strict); Save(fixture);
        var file = TabularFile.Xlsx<ExportRow>(PathFor()).WithWorksheet("data").WithHeaderRow(1).WithFieldTrimming();
        Assert.IsType<XlsxFile<ExportRow>>(file);
        Assert.Equal(1, asynchronous ? await file.AppendAsync(new ExportRow { Value = 4 }) : file.Append(new ExportRow { Value = 4 }));
        Assert.Equal(2, asynchronous ? await file.InsertRangeAsync(1, new[] { new ExportRow { Value = 2 }, new ExportRow { Value = 3 } }) :
            file.InsertRange(1, new[] { new ExportRow { Value = 2 }, new ExportRow { Value = 3 } }));
        Assert.Equal(new[] { 1, 2, 3, 4 }, file.Select(r => r.Value));
        Assert.Equal(1, asynchronous ? await file.InsertAsync(4, new ExportRow { Value = 5 }) : file.Insert(4, new ExportRow { Value = 5 }));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, (await file.ToArrayAsync()).Select(r => r.Value));
        Assert.Equal(strict ? XlsxXml.Strict : XlsxXml.Transitional, Sheet(PathFor()).Name.NamespaceName);
        Clean();
    }

    [Fact]
    public async Task AsyncSequencesAreEnumeratedOnceAndEmptyRangesDoNotPublish()
    {
        TabularFile.XlsxWriter<ExportRow>(PathFor()).Write();
        int enumerations = 0;
        async IAsyncEnumerable<ExportRow> Rows()
        { if (++enumerations != 1) throw new InvalidOperationException(); yield return new() { Value = 2 }; await Task.Yield(); yield return new() { Value = 3 }; }
        var file = TabularFile.Xlsx<ExportRow>(PathFor());
        Assert.Equal(2, await file.AppendRangeAsync(Rows()));
        Assert.Equal(1, await file.InsertRangeAsync(0, AsyncOne(1)));
        Assert.Equal(new[] { 1, 2, 3 }, file.Select(r => r.Value));
        byte[] before = File.ReadAllBytes(PathFor());
        Assert.Equal(0, file.AppendRange([])); Assert.Equal(0, await file.InsertRangeAsync(3, Array.Empty<ExportRow>()));
        Assert.Equal(before, File.ReadAllBytes(PathFor()));
        Assert.Throws<ArgumentOutOfRangeException>(() => file.InsertRange(4, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => file.Insert(-1, new ExportRow()));
        Clean();
    }

    private static async IAsyncEnumerable<ExportRow> AsyncOne(int value) { await Task.Yield(); yield return new() { Value = value }; }

    [Fact]
    public void SparseRowsTitlesAndTrailingFormattedRowsKeepTheirPhysicalPlacement()
    {
        using var fixture = Create(Row(1, Text("A1", "Title")) + Row(4, Text("A4", "Value")) + "<row r=\"5\" ht=\"30\" customHeight=\"1\"/>" +
            Row(7, Number("A7", "1")) + "<row r=\"100\" hidden=\"1\"/>", afterData: "<mergeCells count=\"1\"><mergeCell ref=\"A1:D2\"/></mergeCells>",
            change: parts => parts["xl/worksheets/data.xml"] = parts["xl/worksheets/data.xml"].Replace("<sheetData>",
                "<dimension ref=\"A1:XFD1048576\"/><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"4\" topLeftCell=\"A5\" state=\"frozen\"/><selection activeCell=\"A7\" sqref=\"A7\"/></sheetView></sheetViews><sheetData>", StringComparison.Ordinal));
        Save(fixture);
        var file = TabularFile.Xlsx<ExportRow>(PathFor()).WithHeaderRow(4);
        file.Insert(0, new ExportRow { Value = 2 }); file.Append(new ExportRow { Value = 3 });
        var sheet = Sheet(PathFor()); XNamespace ns = sheet.Name.Namespace;
        Assert.Equal(new[] { "1", "4", "5", "7", "8", "101", "102" }, sheet.Descendants(ns + "row").Select(r => r.Attribute("r")!.Value));
        Assert.Equal("30", sheet.Descendants(ns + "row").Single(r => (string?)r.Attribute("r") == "5").Attribute("ht")!.Value);
        Assert.Equal("A7", sheet.Descendants(ns + "selection").Single().Attribute("activeCell")!.Value);
        Assert.Equal("A5", sheet.Descendants(ns + "pane").Single().Attribute("topLeftCell")!.Value);
        Assert.Equal("A1:D102", sheet.Element(ns + "dimension")!.Attribute("ref")!.Value);
        Assert.Equal(new[] { 2, 1, 3 }, file.Select(r => r.Value)); Clean();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OmittedRowCoordinatesAndPrefixedNamespacesAreHandled(bool asynchronous)
    {
        using var fixture = Create(Row(1, Text("A1", "Value")) + "<row ht=\"20\"><c r=\"A10\"><v>1</v></c></row><row/>", strict: true,
            change: parts => parts["xl/worksheets/data.xml"] = Regex.Replace(parts["xl/worksheets/data.xml"], "(</?)([A-Za-z][A-Za-z0-9]*)", "$1s:$2").Replace("xmlns=", "xmlns:s=", StringComparison.Ordinal));
        Save(fixture); var file = TabularFile.Xlsx<ExportRow>(PathFor());
        Assert.Equal(1, asynchronous ? await file.InsertAsync(0, new ExportRow { Value = 2 }) : file.Insert(0, new ExportRow { Value = 2 }));
        Assert.Equal(new[] { 2, 1 }, file.Select(r => r.Value));
        var sheet = Sheet(PathFor()); XNamespace ns = sheet.Name.Namespace;
        Assert.Equal(new[] { "1", "10", "11", "12" }, sheet.Descendants(ns + "row").Select(r => r.Attribute("r")!.Value));
        Assert.Equal("A11", sheet.Descendants(ns + "c").Last().Attribute("r")!.Value);
    }

    [Fact]
    public void ExistingValuesAreNotConvertedAndNewRowsFollowHeaderOrder()
    {
        using var fixture = Create(Row(1, Text("A1", "Text"), Text("B1", "Number")) + Row(2, Text("A2", "old"), Text("B2", "not-an-int")));
        Save(fixture);
        TabularFile.Xlsx<MutationPair>(PathFor()).Append(new MutationPair { Number = 42, Text = "new" });
        var sheet = Sheet(PathFor()); XNamespace ns = sheet.Name.Namespace;
        var row = sheet.Descendants(ns + "row").Last();
        Assert.Equal("new", row.Descendants(ns + "t").Single().Value);
        Assert.Equal("42", row.Descendants(ns + "v").Single().Value);
        Assert.Contains("not-an-int", sheet.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntouchedSheetsSharedStringsAndWorkbookMetadataKeepTheirPayload(bool strict)
    {
        using var fixture = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "0")), strict, change: parts =>
        {
            SharedStrings(parts, "<si><t>1</t></si>", strict); AddOther(parts, strict, "<sheetData><row r=\"1\"><c t=\"e\"><v>#N/A</v></c></row></sheetData>");
        });
        Save(fixture); var before = Parts(PathFor());
        await TabularFile.Xlsx<ExportRow>(PathFor()).WithSharedStringMemoryBudget(0).WithTemporaryDirectory(directory).AppendAsync(new ExportRow { Value = 2 });
        var after = Parts(PathFor());
        foreach (string part in before.Keys.Where(p => p != "xl/worksheets/data.xml")) Assert.Equal(before[part], after[part]);
        Assert.Equal(new[] { 1, 2 }, TabularFile.Xlsx<ExportRow>(PathFor()).Select(r => r.Value)); Clean();
    }

    private static void AddOther(Dictionary<string, string> parts, bool strict, string body)
    {
        string ns = strict ? XlsxXml.Strict : XlsxXml.Transitional;
        string rel = strict ? XlsxXml.StrictRelationships : XlsxXml.TransitionalRelationships;
        parts["xl/workbook.xml"] = parts["xl/workbook.xml"].Replace("</sheets>", "<sheet name=\"Other\" sheetId=\"2\" r:id=\"other\" state=\"veryHidden\"/></sheets>", StringComparison.Ordinal);
        parts["xl/_rels/workbook.xml.rels"] = parts["xl/_rels/workbook.xml.rels"].Replace("</Relationships>", "<Relationship Id=\"other\" Type=\"" + rel + "/worksheet\" Target=\"worksheets/other.xml\"/></Relationships>", StringComparison.Ordinal);
        parts["[Content_Types].xml"] = parts["[Content_Types].xml"].Replace("</Types>", "<Override PartName=\"/xl/worksheets/other.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>", StringComparison.Ordinal);
        parts["xl/worksheets/other.xml"] = "<worksheet xmlns=\"" + ns + "\">" + body + "</worksheet>";
    }

    [Theory]
    [InlineData("<sheetData><row><c><f>PRIVATE_FORMULA</f><v>1</v></c></row></sheetData>")]
    [InlineData("<sheetData/><tableParts/>")]
    [InlineData("<sheetData/><drawing/>")]
    public void UnsupportedFeaturesOnOtherSheetsRejectEvenEmptyRanges(string other)
    {
        using var fixture = Create(SingleColumn("1"), change: parts => AddOther(parts, false, other)); Save(fixture);
        var before = File.ReadAllBytes(PathFor());
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<ExportRow>(PathFor()).AppendRange([]));
        Assert.DoesNotContain("PRIVATE_FORMULA", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(PathFor())); Clean();
    }

    [Fact]
    public async Task ErrorsAndCancellationLeaveTheWorkbookAndTemporaryDirectoryUnchanged()
    {
        using var fixture = Create(SingleColumn("1")); Save(fixture); var before = File.ReadAllBytes(PathFor());
        var file = TabularFile.Xlsx<ExportRow>(PathFor()).WithTemporaryDirectory(directory);
        var failure = new InvalidOperationException("input failed");
        IEnumerable<ExportRow> Broken() { yield return new() { Value = 2 }; throw failure; }
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => file.AppendRange(Broken())));
        using var cancellation = new CancellationTokenSource();
        async IAsyncEnumerable<ExportRow> Cancelled()
        { yield return new() { Value = 2 }; await Task.Yield(); cancellation.Cancel(); yield return new() { Value = 3 }; }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.InsertRangeAsync(0, Cancelled(), cancellation.Token).AsTask());
        Assert.Equal(before, File.ReadAllBytes(PathFor())); Clean();
        Assert.Throws<XlsxFormatException>(() => file.WithTemporaryFiles(false).AppendRange([]));
        Assert.Throws<XlsxFormatException>(() => file.WithMaxTemporaryBytes(0).Append(new ExportRow { Value = 2 }));
        Assert.Throws<XlsxFormatException>(() => file.WithMaxDecompressedBytes(0).AppendRange([]));
        Assert.Equal(before, File.ReadAllBytes(PathFor())); Clean();
    }

    [Fact]
    public async Task ArchiveRewritingUsesOnlyAsyncDestinationIo()
    {
        using var original = Create(SingleColumn("1"));
        await using var prepared = await XlsxMutationPreparation<ExportRow>.OpenAsync(original, new XlsxOptions(), 0, CancellationToken.None);
        prepared.Output.Row(new ExportRow { Value = 2 }, 1, prepared.InsertionRow);
        using var spool = new MemoryStream(Encoding.UTF8.GetBytes(prepared.Output.Record.ToString()));
        using var target = new ExportProbeStream(asyncOnly: true);
        await XlsxMutationArchive.CopyAsync(prepared.Package, prepared.Metadata, target, spool, prepared.InsertionRow, 1,
            prepared.LastRow, prepared.MaxColumn, CancellationToken.None);
        using var result = new MemoryStream(target.Bytes);
        Assert.Equal(new[] { 2, 1 }, TabularFile.Xlsx<ExportRow>(result).Select(r => r.Value));
        Assert.Equal(0, target.SyncWrites); Assert.Equal(0, target.SyncFlushes);
    }
}

[TabularRecord] public sealed partial class MutationPair { public int Number { get; init; } public string Text { get; init; } = ""; }
