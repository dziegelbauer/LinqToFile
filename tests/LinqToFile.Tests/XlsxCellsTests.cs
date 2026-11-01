using LinqToFile.Formats.Xlsx;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxCellsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SharedStringsResolveHeadersAndRichUnicodeValuesInBothStores(bool strict, bool spill)
    {
        using var stream = Create(Row(1, Cell("A1", "s", "0")) + Row(2, Cell("A2", "s", "1")), strict,
            change: p => SharedStrings(p, "<si><t>Value</t></si><si><r><rPr><b/></rPr><t>雪😀</t></r><r><t>_x000D_ end</t></r><rPh sb=\"0\" eb=\"1\"><t>ignore</t></rPh></si>", strict));
        Assert.Equal("雪😀\r end", TabularFile.Xlsx<SheetString>(stream).WithSharedStringMemoryBudget(spill ? 0 : 1024).Single().Value);
    }

    [Theory]
    [InlineData("_x0041_", "A")]
    [InlineData("_x005F_x0041_", "_x0041_")]
    [InlineData("_x0000_", "\0")]
    [InlineData("_xD83D__xDE00_", "😀")]
    [InlineData("_xZZZZ_ _x041_ _x00041_", "_xZZZZ_ _x041_ _x00041_")]
    public void DecodesEscapesOnceForInlineAndCachedStrings(string encoded, string expected)
    {
        using var inline = Create(Row(1, Text("A1", "Value")) + Row(2, Text("A2", encoded)));
        using var cached = Create(SingleColumn(encoded, "str"));
        Assert.Equal(expected, TabularFile.Xlsx<SheetString>(inline).Single().Value);
        Assert.Equal(expected, TabularFile.Xlsx<SheetString>(cached).Single().Value);
    }

    [Fact]
    public void EscapeSequencesCannotCrossRunBoundariesButCanCrossXmlTextChunks()
    {
        using var split = Create(Row(1, Text("A1", "Value")) + Row(2, "<c r=\"A2\" t=\"inlineStr\"><is><r><t>_x00</t></r><r><t>41_</t></r></is></c>"));
        Assert.Equal("_x0041_", TabularFile.Xlsx<SheetString>(split).Single().Value);
        string prefix = new('a', 1022);
        using var boundary = Create(Row(1, Text("A1", "Value")) + Row(2, Text("A2", prefix + "_x0041_")));
        Assert.Equal(prefix + "A", TabularFile.Xlsx<SheetString>(boundary).Single().Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpillPreservesRawUtf16IncludingUnpairedSurrogates(bool spill)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "0")),
            change: p => SharedStrings(p, "<si><t>_xD800_</t></si>"));
        Assert.Equal("\uD800", TabularFile.Xlsx<SheetString>(stream).WithSharedStringMemoryBudget(spill ? 0 : 1024).Single().Value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("bogus")]
    [InlineData("999999999999999999999999")]
    public void InvalidSharedReferencesHaveCellContext(string index)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", index)),
            change: p => SharedStrings(p, "<si><t>x</t></si>"));
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(stream).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void EmptySharedStringRowsAreSkippedButEmptyFormulaResultsAreRecords()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "0")) +
            Row(3, "<c r=\"A3\" t=\"str\"><f>\"\"</f><v/></c>"), change: p => SharedStrings(p, "<si><t/></si>"));
        Assert.Equal("", TabularFile.Xlsx<SheetString>(stream).Single().Value);
    }

    [Theory]
    [InlineData("<c r=\"A2\"><f>1+1</f></c>")]
    [InlineData("<c r=\"A2\"><f>1+1</f><v/></c>")]
    [InlineData("<c r=\"A2\" t=\"b\"><f>1=1</f><v/></c>")]
    [InlineData("<c r=\"A2\" t=\"d\"><f>NOW()</f><v/></c>")]
    public void MissingCachedResultsThrowWithoutFormulaText(string cell)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, cell));
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(stream).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.DoesNotContain("1+1", error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void FormulaHeadersAndSharedFormulaFollowersUseOnlyCachedValues()
    {
        using var stream = Create(Row(1, "<c r=\"A1\" t=\"str\"><f>unsupportedFunction()</f><v>Value</v></c>") +
            Row(2, "<c r=\"A2\"><f t=\"shared\" si=\"0\" ref=\"A2:A3\">1+1</f><v>2</v></c>") +
            Row(3, "<c r=\"A3\"><f t=\"shared\" si=\"0\"/><v>3</v></c>"));
        Assert.Equal(new[] { 2, 3 }, TabularFile.Xlsx<SheetInteger>(stream).Select(row => row.Value));
    }

    [Fact]
    public void EmptyFormulaBeyondTheHeaderStillThrows()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Number("A2", "42"), "<c r=\"B2\" t=\"str\"><f>\"\"</f><v/></c>"));
        Assert.Equal("B2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single()).CellReference);
    }

    [Fact]
    public void SharedMemoryAndDiskIndexAccountingAreBounded()
    {
        string items = string.Concat(Enumerable.Range(0, 2000).Select(i => $"<si><t>{i:D8}</t></si>"));
        using var stream = Create(Row(1, Text("A1", "Value")), change: p => SharedStrings(p, items));
        using var package = new XlsxPackage(stream, new XlsxOptions { SharedStringMemoryBudget = 256 });
        Assert.True(package.SharedStrings!.Spilled);
        Assert.Equal(2000, package.SharedStrings.Count);
        Assert.Equal(0, package.SharedStrings.AccountedMemoryBytes);
        Assert.Equal(2000 * (12 + 16), package.Resources.TemporaryBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RandomSharedLookupsAndOwnedValuesSurviveBufferReuseAndDisposal(bool spill)
    {
        string items = string.Concat(Enumerable.Range(0, 2000).Select(i => $"<si><t> {i:D8} </t></si>"));
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "1999")) +
            Row(3, Cell("A3", "s", "0")) + Row(4, Cell("A4", "s", "1000")), change: p => SharedStrings(p, items));
        var records = TabularFile.Xlsx<SheetString>(stream).WithSharedStringMemoryBudget(spill ? 0 : 1_000_000).WithFieldTrimming().ToArray();
        Assert.Equal(new[] { "00001999", "00000000", "00001000" }, records.Select(r => r.Value));
    }

    [Theory]
    [InlineData("_x0044_ata", "Data")]
    [InlineData("_x005F_x0041_", "_x0041_")]
    public void WorksheetNamesUseOnePassSpreadsheetDecoding(string encoded, string name)
    {
        using var stream = Create(SingleColumn("42"), change: p => p["xl/workbook.xml"] =
            p["xl/workbook.xml"].Replace("name=\"Data\"", $"name=\"{encoded}\"", StringComparison.Ordinal));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).WithWorksheet(name).Single().Value);
    }

    [Theory]
    [InlineData("<c r=\"A2\"><v>42</v>secret</c>")]
    [InlineData("<c r=\"A2\" t=\"inlineStr\"><is>secret<t>x</t></is></c>")]
    public void UnexpectedTextInStructuredCellMarkupIsNotSilentlyDropped(string cell)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, cell));
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(stream).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }
}
