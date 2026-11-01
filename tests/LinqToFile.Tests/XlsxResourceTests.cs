using LinqToFile.Formats.Xlsx;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxResourceTests
{
    [Fact]
    public void WorkbookLimitCountsTheArchiveFromTheCurrentOrigin()
    {
        using var book = Create(SingleColumn("42"));
        using var exact = new MemoryStream();
        exact.Write(new byte[20]);
        long origin = exact.Position;
        book.CopyTo(exact);
        exact.Position = origin;
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(exact).WithMaxWorkbookBytes(book.Length).Single().Value);
        using var shortLimit = new MemoryStream(book.ToArray());
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(shortLimit).WithMaxWorkbookBytes(book.Length - 1).Single());
    }

    [Fact]
    public void DecompressedLimitIncludesMetadataPreflightAndReopenedWorksheet()
    {
        using var book = Create(SingleColumn("42"));
        long setup;
        using (var package = new XlsxPackage(book, new XlsxOptions())) setup = package.Resources.DecompressedBytes;
        book.Position = 0;
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(book).WithMaxDecompressedBytes(setup).Single());
        using var measure = Create(SingleColumn("42"));
        long all;
        using (var package = new XlsxPackage(measure, new XlsxOptions()))
        {
            using var reader = new XlsxRecordReader(package, new XlsxOptions());
            while (reader.Read()) { }
            all = package.Resources.DecompressedBytes;
        }
        Assert.True(all > setup);
        using var exact = Create(SingleColumn("42"));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(exact).WithMaxDecompressedBytes(all).Single().Value);
        using var shortLimit = Create(SingleColumn("42"));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(shortLimit).WithMaxDecompressedBytes(all - 1).Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ZeroResourceLimitsAreValidAndThrowOnNonEmptyWorkbooks(int kind)
    {
        using var book = Create(SingleColumn("42"));
        var source = TabularFile.Xlsx<SheetInteger>(book);
        source = kind switch
        {
            0 => source.WithMaxWorkbookBytes(0),
            1 => source.WithMaxDecompressedBytes(0),
            2 => source.WithMaxCellTextLength(0),
            _ => source.WithMaxTemporaryBytes(0)
        };
        if (kind == 3) Assert.Equal(42, source.Single().Value); // No temporary storage is needed.
        else Assert.Throws<XlsxFormatException>(() => source.Single());
    }

    [Fact]
    public void CellLimitCountsResolvedTextBeforeTrimmingAndAcrossRichRuns()
    {
        using var encoded = Create(Row(1, Text("A1", "Value")) + Row(2, Text("A2", "_x0041__x0042__x0043__x0044__x0045_")));
        Assert.Equal("ABCDE", TabularFile.Xlsx<SheetString>(encoded).WithMaxCellTextLength(5).Single().Value);
        using var whitespace = Create(Row(1, Text("A1", "Value")) + Row(2, Text("A2", "  abc ")));
        Assert.Equal("A2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(whitespace)
            .WithFieldTrimming().WithMaxCellTextLength(5).Single()).CellReference);
        using var runs = Create(Row(1, Text("A1", "Value")) + Row(2,
            "<c r=\"A2\" t=\"inlineStr\"><is><r><t>abc</t></r><r><t>def</t></r></is></c>"));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(runs).WithMaxCellTextLength(5).Single());
    }

    [Fact]
    public void CellLimitsCoverSharedStringsAndUnmappedColumns()
    {
        using var shared = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "0")),
            change: p => SharedStrings(p, "<si><t>abcdef</t></si>"));
        var sharedError = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(shared).WithMaxCellTextLength(5).Single());
        Assert.Equal("xl/sharedStrings.xml", sharedError.PartName);
        using var unused = Create(Row(1, Text("A1", "Value"), Text("B1", "Other")) + Row(2, Number("A2", "42"), Text("B2", "abcdef")));
        Assert.Equal("B2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(unused).WithMaxCellTextLength(5).Single()).CellReference);
    }

    [Fact]
    public void TemporaryLimitIncludesBothStringDataAndTheDiskIndex()
    {
        using var exact = Create(SingleColumn("42"), change: p => SharedStrings(p, "<si><t>abc</t></si>"));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(exact).WithSharedStringMemoryBudget(0).WithMaxTemporaryBytes(18).Single().Value);
        using var shortLimit = Create(SingleColumn("42"), change: p => SharedStrings(p, "<si><t>abc</t></si>"));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(shortLimit).WithSharedStringMemoryBudget(0).WithMaxTemporaryBytes(17).Single());
    }

    [Fact]
    public void NoTempModeNeverTouchesTheTemporaryDirectoryAndDoesNotFallBackToUnboundedRam()
    {
        string missing = Path.Combine(Path.GetTempPath(), "LinqToFile-missing-" + Guid.NewGuid());
        using var small = Create(SingleColumn("42"), change: p => SharedStrings(p, "<si><t>x</t></si>"));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(small).WithTemporaryDirectory(missing).WithTemporaryFiles(false).Single().Value);
        using var spill = Create(SingleColumn("42"), change: p => SharedStrings(p, "<si><t>x</t></si>"));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(spill).WithTemporaryDirectory(missing)
            .WithTemporaryFiles(false).WithSharedStringMemoryBudget(0).Single());
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("early")]
    [InlineData("conversion")]
    [InlineData("setup")]
    [InlineData("limit")]
    public void TemporaryFilesAreRemovedOnEveryOwnedLifetimeExit(string mode)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-cleanup-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var book = Create(Row(1, Cell("A1", "s", "0")) + Row(2, Cell("A2", "s", mode == "conversion" ? "2" : "1")),
                change: p => SharedStrings(p, "<si><t>Value</t></si><si><t>42</t></si><si><t>bad</t></si>" +
                    (mode == "setup" ? "<si><r/></si>" : "")));
            var source = TabularFile.Xlsx<SheetInteger>(book).WithTemporaryDirectory(directory).WithSharedStringMemoryBudget(0);
            if (mode == "conversion") Assert.Throws<TabularConversionException>(() => source.ToArray());
            else if (mode == "setup") Assert.Throws<XlsxFormatException>(() => source.ToArray());
            else if (mode == "limit") Assert.Throws<XlsxFormatException>(() => source.WithMaxTemporaryBytes(20).ToArray());
            else if (mode == "early") Assert.Equal(42, source.Take(1).Single().Value);
            else Assert.Equal(42, source.Single().Value);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ResourceConfigurationValidatesImmediatelyWithoutClaimingTheStream()
    {
        using var stream = Create(SingleColumn("42"));
        var source = TabularFile.Xlsx<SheetInteger>(stream);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxWorkbookBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxDecompressedBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxCellTextLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxTemporaryBytes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithSharedStringMemoryBudget(-1));
        Assert.Throws<ArgumentException>(() => source.WithTemporaryDirectory(" "));
        Assert.Equal(42, source.WithMaxWorkbookBytes(null).WithMaxCellTextLength(null).Single().Value);
    }
}
