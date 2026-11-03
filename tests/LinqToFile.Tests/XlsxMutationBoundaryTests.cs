using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using LinqToFile.Formats.Xlsx;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxMutationBoundaryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-MutationBounds-" + Guid.NewGuid().ToString("N"));
    public XlsxMutationBoundaryTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string PathFor => Path.Combine(directory, "data.xlsx");
    private void Save(MemoryStream source) => File.WriteAllBytes(PathFor, source.ToArray());
    private void Clean() => Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

    [Fact]
    public void MutationFormattingCachesNativeStylesWithoutPerRowAllocations()
    {
        var styles = new XlsxMutationStyles(null, XlsxXml.Strict);
        var output = new XlsxOutput<MutationDate>(new XlsxWriteOptions(),
            new XlsxMutationLayout([0], ["Value"], "xl/worksheets/data.xml", XlsxXml.Strict, styles));
        var row = new MutationDate { Value = new DateOnly(2026, 11, 3) };
        Measure(20_000);
        long allocated = long.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++) allocated = Math.Min(allocated, Measure(100_000));
        Assert.InRange(allocated, 0, 512);
        Assert.Equal(4, styles.Root!.Element(XName.Get("cellXfs", XlsxXml.Strict))!.Elements().Count());

        long Measure(int count)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 1; index <= count; index++) output.Row(row, index, index + 100);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MissingStylesAreAddedUsingTheExistingVariantAndDateSystem(bool strict, bool date1904)
    {
        using var source = Create(SingleColumn("1"), strict, change: p =>
            p["xl/workbook.xml"] = p["xl/workbook.xml"].Replace("<sheets>", "<workbookPr date1904=\"" + (date1904 ? "1" : "0") + "\"/><sheets>", StringComparison.Ordinal));
        Save(source);
        var date = new DateOnly(2026, 11, 3);
        var file = TabularFile.Xlsx<MutationDate>(PathFor).WithCulture(CultureInfo.GetCultureInfo("fr-FR"));
        Assert.Equal(1, await file.AppendAsync(new MutationDate { Value = date }));
        Assert.Equal(date, file.Last().Value);
        using var zip = ZipFile.OpenRead(PathFor);
        using var styles = zip.GetEntry("xl/styles-linqtofile.xml")!.Open();
        var root = XElement.Load(styles);
        Assert.Equal(strict ? XlsxXml.Strict : XlsxXml.Transitional, root.Name.NamespaceName);
        Clean();
    }

    [Fact]
    public async Task NewDateStylesAppendWithoutChangingOldIndexesOrFormats()
    {
        TabularFile.XlsxWriter<MutationDate>(PathFor).WithDateSystem(TabularDateSystem.Excel1904).Write(new MutationDate { Value = new(2026, 11, 1) });
        using (var zip = ZipFile.Open(PathFor, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("xl/styles.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/styles.xml").Open());
            writer.Write(xml.Replace("yyyy-mm-dd\"", "dd/mm/yyyy\"", StringComparison.Ordinal));
        }
        await TabularFile.Xlsx<MutationDate>(PathFor).AppendAsync(new MutationDate { Value = new(2026, 11, 2) });
        using var result = ZipFile.OpenRead(PathFor);
        using var stylePart = result.GetEntry("xl/styles.xml")!.Open();
        var styles = XElement.Load(stylePart); XNamespace ns = styles.Name.Namespace;
        Assert.Equal(5, styles.Element(ns + "cellXfs")!.Elements().Count());
        Assert.Contains(styles.Descendants(ns + "numFmt"), f => (string?)f.Attribute("numFmtId") == "164" && (string?)f.Attribute("formatCode") == "dd/mm/yyyy");
        using var sheetPart = result.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XElement.Load(sheetPart);
        Assert.Equal(new[] { "1", "4" }, sheet.Descendants(ns + "c").Skip(1).Select(c => c.Attribute("s")!.Value));
    }

    [Fact]
    public async Task OverlappingWritesShareTheGuardAcrossWorksheetsAndFormats()
    {
        TabularFile.XlsxWriter<ExportRow>(PathFor).Write();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<ExportRow> Rows()
        { entered.SetResult(); await release.Task; yield return new() { Value = 1 }; }
        var pending = TabularFile.Xlsx<ExportRow>(PathFor).AppendRangeAsync(Rows()).AsTask();
        await entered.Task;
        try
        {
            Assert.Throws<InvalidOperationException>(() => TabularFile.Xlsx<ExportRow>(PathFor).WithWorksheet("Other").Append(new ExportRow()));
            Assert.Throws<InvalidOperationException>(() => TabularFile.Csv<ExportRow>(PathFor).Append(new ExportRow()));
        }
        finally { release.SetResult(); }
        Assert.Equal(1, await pending); Clean();
    }

    [Fact]
    public async Task CancellationAtInputDisposalStillPreventsPublication()
    {
        TabularFile.XlsxWriter<ExportRow>(PathFor).Write(new ExportRow { Value = 1 });
        var before = File.ReadAllBytes(PathFor);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TabularFile.Xlsx<ExportRow>(PathFor)
            .WithTemporaryDirectory(directory).AppendRangeAsync(new CancelAtDisposal(cancellation), cancellation.Token).AsTask());
        Assert.Equal(before, File.ReadAllBytes(PathFor)); Clean();
    }

    private sealed class CancelAtDisposal(CancellationTokenSource cancellation) : IAsyncEnumerable<ExportRow>, IAsyncEnumerator<ExportRow>
    {
        private bool yielded;
        public ExportRow Current { get; } = new() { Value = 2 };
        public IAsyncEnumerator<ExportRow> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() { bool result = !yielded; yielded = true; return new(result); }
        public ValueTask DisposeAsync() { cancellation.Cancel(); return default; }
    }

    [Theory]
    [InlineData("<sheetProtection/>")]
    [InlineData("<mergeCells><mergeCell ref=\"B2:D2\"/></mergeCells>")]
    [InlineData("<unknown/>")]
    public void SelectedUnsupportedContentIsRejectedEvenOutsideMappedColumns(string after)
    {
        using var source = Create(SingleColumn("1"), afterData: after); Save(source);
        var before = File.ReadAllBytes(PathFor);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<ExportRow>(PathFor).AppendRange([]));
        Assert.Equal(before, File.ReadAllBytes(PathFor)); Clean();
    }

    [Fact]
    public void PhysicalLimitsAndOutputQuotasPreserveTheOriginal()
    {
        using var source = Create(SingleColumn("1") + "<row r=\"1048576\" hidden=\"1\"/>"); Save(source);
        var before = File.ReadAllBytes(PathFor);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<ExportRow>(PathFor).Insert(0, new ExportRow { Value = 2 }));
        Assert.Equal(0, TabularFile.Xlsx<ExportRow>(PathFor).AppendRange([]));
        Assert.Equal(before, File.ReadAllBytes(PathFor)); Clean();
        using var plain = Create(SingleColumn("1", "str")); Save(plain);
        before = File.ReadAllBytes(PathFor);
        string large = string.Concat(Enumerable.Range(0, 250).Select(_ => Guid.NewGuid().ToString("N")));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<WriteText>(PathFor).WithMaxWorkbookBytes(before.Length + 10)
            .WithTemporaryDirectory(directory).Append(new WriteText { Value = large }));
        Assert.Equal(before, File.ReadAllBytes(PathFor)); Clean();
    }

    [Fact]
    public void MissingFilesColumnsAndInvalidNativeCellsFailBeforeNewInput()
    {
        Assert.Throws<FileNotFoundException>(() => TabularFile.Xlsx<ExportRow>(PathFor).AppendRange([]));
        using var extra = Create(Row(1, Text("A1", "Value"), Text("B1", "Extra"))); Save(extra);
        Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<ExportRow>(PathFor).AppendRange([]));
        using var malformed = Create(SingleColumn("not-a-number")); Save(malformed);
        var before = File.ReadAllBytes(PathFor);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<ExportRow>(PathFor).AppendRange([]));
        Assert.Equal(before, File.ReadAllBytes(PathFor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownSharedStringMarkupIsRejectedBeforePublication(bool asynchronous)
    {
        using var source = Create(SingleColumn("1"), change: parts => SharedStrings(parts,
            "<si><r><rPr><unknown ref=\"A2\"/></rPr><t>unused</t></r></si>"));
        Save(source);
        var before = File.ReadAllBytes(PathFor);
        var file = TabularFile.Xlsx<ExportRow>(PathFor);
        if (asynchronous) await Assert.ThrowsAsync<XlsxFormatException>(() => file.AppendRangeAsync(Array.Empty<ExportRow>()).AsTask());
        else Assert.Throws<XlsxFormatException>(() => file.AppendRange([]));
        Assert.Equal(before, File.ReadAllBytes(PathFor));
    }
}

[TabularRecord] public sealed partial class MutationDate { public DateOnly Value { get; init; } }
