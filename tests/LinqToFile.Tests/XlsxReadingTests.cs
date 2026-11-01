using System.Globalization;
using System.IO.Compression;
using System.Text;
using LinqToFile.Tests.Support;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxReadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsTypedSparseRowsAndRetainsOwnedStrings(bool strict)
    {
        using var stream = Create(Row(3, Text("A3", "Id"), Text("B3", "Name"), Text("C3", "Active"), Text("D3", "Amount")) +
            Row(5, Number("A5", "42.0"), Text("B5", " Ada 雪😀 "), Cell("C5", "b", "1")) +
            Row(8, Number("A8", "7e0"), Text("B8", "Grace"), Cell("C8", "b", "0"), Number("D8", "12.50")), strict);
        var rows = TabularFile.Xlsx<SheetPerson>(stream).WithWorksheet("dAtA").ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(42, rows[0].Id);
        Assert.Equal(" Ada 雪😀 ", rows[0].Name);
        Assert.True(rows[0].Active);
        Assert.Null(rows[0].Amount);
        Assert.Equal(12.5m, rows[1].Amount);
        Assert.False(rows[1].Active);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void UsesConfiguredHeaderRowAndAllowsMergedTitle()
    {
        using var stream = Create(Row(1, Text("A1", "Title")) + Row(4, Text("A4", " value ")) + Row(7, Number("A7", "42")),
            afterData: "<mergeCells><mergeCell ref=\"A1:D2\"/></mergeCells>");
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).WithHeaderRow(4).WithHeaderTrimming()
            .WithHeaderComparison(StringComparer.OrdinalIgnoreCase).Single().Value);
    }

    [Theory]
    [InlineData("A1:B1")]
    [InlineData("A2:B2")]
    [InlineData("A100:B100")]
    public void RejectsIntersectingMergesBeforeReturningHeaderOrData(string range)
    {
        using var stream = Create(SingleColumn("42"), afterData: $"<mergeCells><mergeCell ref=\"{range}\"/></mergeCells>");
        using var enumerator = TabularFile.Xlsx<SheetInteger>(stream).GetEnumerator();
        var error = Assert.Throws<XlsxFormatException>(() => enumerator.MoveNext());
        Assert.Equal("Data", error.WorksheetName);
    }

    [Fact]
    public void MergesOutsideTheHeaderColumnsDoNotAffectTheTable()
    {
        using var stream = Create(SingleColumn("42"), afterData: "<mergeCells><mergeCell ref=\"B1:D20\"/></mergeCells>");
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).Single().Value);
    }

    [Theory]
    [InlineData("42.0", 42)]
    [InlineData("4.2E1", 42)]
    [InlineData("420000000000000000000000000000000000000000000E-43", 42)]
    [InlineData("-2147483648", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    public void NativeIntegersAreExactAndCultureIndependent(string value, int expected)
    {
        using var stream = Create(SingleColumn(value));
        Assert.Equal(expected, TabularFile.Xlsx<SheetInteger>(stream).WithCulture(CultureInfo.GetCultureInfo("fr-FR")).Single().Value);
    }

    [Theory]
    [InlineData("42.1")]
    [InlineData("42.000000000000000000000000000001")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("1e1000")]
    public void RejectsIntegerRoundingAndOverflowWithCellContext(string value)
    {
        using var stream = Create(SingleColumn(value));
        var error = Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
        Assert.Equal("Data", error.WorksheetName);
        Assert.Equal(2, error.WorksheetRowNumber);
        Assert.Equal("A2", error.CellReference);
        Assert.Equal(2, error.RecordNumber);
        Assert.Equal(0, error.LineNumber);
        Assert.Equal(0, error.FailureLineNumber);
        Assert.Equal("Value", error.TargetMember);
        Assert.Equal("xl/worksheets/data.xml", error.PartName);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void TextConversionsUseCultureWhileNativeValuesUseInvariantRules()
    {
        using var native = Create(SingleColumn("12.50"));
        using var text = Create(SingleColumn("12,50", "str"));
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal(12.5m, TabularFile.Xlsx<SheetDecimal>(native).WithCulture(culture).Single().Value);
        Assert.Equal(12.5m, TabularFile.Xlsx<SheetDecimal>(text).WithCulture(culture).Single().Value);
    }

    [Fact]
    public void NativeNumericMappingDoesNotRoundThroughDouble()
    {
        using var stream = Create(Row(1, Text("A1", "Signed"), Text("B1", "Unsigned"), Text("C1", "Price"), Text("D1", "Small")) +
            Row(2, Number("A2", "170141183460469231731687303715884105727"),
                Number("B2", "340282366920938463463374607431768211455"),
                Number("C2", "9007199254740993.25"), Number("D2", "1.5")));
        var row = TabularFile.Xlsx<SheetNativeNumbers>(stream).Single();
        Assert.Equal(Int128.MaxValue, row.Signed);
        Assert.Equal(UInt128.MaxValue, row.Unsigned);
        Assert.Equal(9007199254740993.25m, row.Price);
        Assert.Equal((Half)1.5, row.Small);
    }

    [Fact]
    public void NativeNumericOverflowToInfinityIsRejected()
    {
        using var stream = Create(SingleColumn("1e1000"));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDouble>(stream).Single());
    }

    [Theory]
    [InlineData("b", "1", "true")]
    [InlineData("b", "0", "false")]
    [InlineData("n", "1.20E2", "1.20E2")]
    [InlineData("str", " raw ", " raw ")]
    public void StringTargetsUseTheAgreedRepresentations(string type, string value, string expected)
    {
        using var stream = Create(SingleColumn(value, type));
        Assert.Equal(expected, TabularFile.Xlsx<SheetString>(stream).Single().Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    public void OrdinaryNumbersDoNotBecomeBooleans(string value)
    {
        using var stream = Create(SingleColumn(value));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetBoolean>(stream).Single());
    }

    [Fact]
    public void NativeBooleanDoesNotBecomeNumberOrEnumAndNumericDoesNotBecomeCharOrGuid()
    {
        using var a = Create(SingleColumn("1", "b"));
        using var b = Create(SingleColumn("1", "b"));
        using var c = Create(SingleColumn("1"));
        using var d = Create(SingleColumn("00000000000000000000000000000001"));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetInteger>(a).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetEnum>(b).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetCharacter>(c).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetGuid>(d).Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("<row r=\"1\"/>")]
    [InlineData("<row r=\"1\"><c r=\"A1\" s=\"99\"/></row>")]
    public void EmptySheetsThrowForMissingHeader(string rows)
    {
        using var stream = Create(rows);
        var error = Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(stream).ToArray());
        Assert.Equal("Data", error.WorksheetName);
        Assert.Equal(0, error.LineNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void MissingExplicitHeaderDoesNotSearchOnward(int headerRow)
    {
        using var stream = Create(Row(2, Text("A2", "Value")) + Row(4, Number("A4", "42")));
        var error = Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(stream).WithHeaderRow(headerRow).ToArray());
        Assert.Equal(headerRow, error.WorksheetRowNumber);
    }

    [Fact]
    public void HeaderOnlyStillBindsAndChecksMissingProperties()
    {
        using var valid = Create(Row(1, Text("A1", "Value")));
        Assert.Empty(TabularFile.Xlsx<SheetInteger>(valid));
        using var missing = Create(Row(1, Text("A1", "Other")));
        var error = Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(missing).ToArray());
        Assert.Equal("Value", error.TargetMember);
        Assert.Equal(1, error.WorksheetRowNumber);
    }

    [Theory]
    [InlineData("Value", "Value")]
    [InlineData("Value", " ")]
    [InlineData("", "Value")]
    public void RejectsInvalidHeaders(string first, string second)
    {
        using var stream = Create(Row(1, Text("A1", first), Text("B1", second)));
        Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(stream).ToArray());
    }

    [Fact]
    public void HeaderGapsThrowAndTrailingFormattingIsIgnored()
    {
        using var gap = Create(Row(1, Text("A1", "Value"), Text("C1", "Extra")));
        Assert.Equal("B1", Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(gap).ToArray()).CellReference);
        using var trailing = Create(Row(1, Text("A1", "Value"), "<c r=\"XFD1\" s=\"1\"/>") + Row(2, Number("A2", "42"), "<c r=\"XFD2\"/>"));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(trailing).Single().Value);
    }

    [Fact]
    public void BlankRowsAreSkippedButWhitespaceIsContentBeforeTrimming()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Text("A2", "")) + Row(3) + Row(4, Text("A4", " ")));
        Assert.Equal("", TabularFile.Xlsx<SheetString>(stream).WithFieldTrimming().Single().Value);
    }

    [Fact]
    public void ValuesOutsideTheHeaderThrowEvenForUnmappedColumns()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Number("A2", "42"), Number("C2", "0")));
        Assert.Equal("C2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single()).CellReference);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("INF")]
    [InlineData("1,2")]
    [InlineData("1x")]
    [InlineData("1e")]
    public void MalformedNativeNumbersThrowEvenWhenUnmapped(string value)
    {
        using var stream = Create(Row(1, Text("A1", "Value"), Text("B1", "Unused")) + Row(2, Number("A2", "42"), Number("B2", value)));
        Assert.Equal("B2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single()).CellReference);
    }

    [Theory]
    [InlineData("s", "0")]
    [InlineData("e", "#N/A")]
    [InlineData("b", "true")]
    public void UnsupportedOrInvalidCellFeaturesFailExplicitly(string type, string value)
    {
        using var stream = Create(SingleColumn(value, type));
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(stream).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsCachedFormulaAndRichText()
    {
        using var formula = Create(Row(1, Text("A1", "Value")) + Row(2, "<c r=\"A2\"><f>1+1</f><v>2</v></c>"));
        Assert.Equal(2, TabularFile.Xlsx<SheetInteger>(formula).Single().Value);
        using var rich = Create(Row(1, Text("A1", "Value")) + Row(2, "<c r=\"A2\" t=\"inlineStr\"><is><r><t>x</t></r></is></c>"));
        Assert.Equal("x", TabularFile.Xlsx<SheetString>(rich).Single().Value);
    }

    [Fact]
    public void InfersOmittedCoordinatesWithoutShiftingSparseExplicitCells()
    {
        using var stream = Create("<row><c t=\"inlineStr\"><is><t>Value</t></is></c></row><row><c r=\"A5\"><v>42</v></c></row><row><c><v>7</v></c></row>");
        Assert.Equal(new[] { 42, 7 }, TabularFile.Xlsx<SheetInteger>(stream).Select(r => r.Value));
    }

    public static IEnumerable<object[]> BadCoordinates()
    {
        foreach (string cell in new[] { "A0", "A1048577", "XFE2", "AAAA2", "$A$2", "A02", "a2", "A1" })
            yield return [Row(2, Number(cell, "42"))];
        yield return [Row(2, Number("A2", "42"), Number("A2", "7"))];
        yield return [Row(2, Number("B2", "42"), Number("A2", "7"))];
        yield return [Row(2, Number("A2", "42")) + Row(2, Number("A2", "7"))];
        yield return [Row(3, Number("A3", "42")) + Row(2, Number("A2", "7"))];
    }

    [Theory]
    [MemberData(nameof(BadCoordinates))]
    public void RejectsInconsistentDuplicateOrOutOfRangeCoordinates(string data)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + data);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).ToArray());
    }

    [Fact]
    public void TakeDoesNotConvertLaterRows()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Number("A2", "42")) + Row(3, Number("A3", "0.1")));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).Take(1).Single().Value);
    }

    [Fact]
    public void StreamStartsAtItsCurrentPositionAndConfigurationsShareOneClaim()
    {
        using var book = Create(SingleColumn("42"));
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("prefix bytes"));
        long origin = stream.Position;
        book.CopyTo(stream);
        stream.Position = origin;
        var source = TabularFile.Xlsx<SheetInteger>(stream);
        using (source.GetEnumerator()) { }
        Assert.Equal(42, source.WithWorksheet("Data").Single().Value);
        Assert.True(stream.CanRead);
        Assert.Throws<InvalidOperationException>(() => source.ToArray());
    }

    [Fact]
    public void OwnedStreamsCloseOnFailureAndEarlyExit()
    {
        var valid = Create(SingleColumn("42"));
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(valid, leaveOpen: false).Take(1).Single().Value);
        Assert.False(valid.CanRead);
        var invalid = Create("<row r=\"1\"/>");
        Assert.Throws<TabularSchemaException>(() => TabularFile.Xlsx<SheetInteger>(invalid, leaveOpen: false).ToArray());
        Assert.False(invalid.CanRead);
    }

    [Fact]
    public void PathsAreDeferredAndRepeatable()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        var source = TabularFile.Xlsx<SheetInteger>(path);
        Assert.Throws<FileNotFoundException>(() => source.ToArray());
        try
        {
            using (var stream = Create(SingleColumn("42"))) File.WriteAllBytes(path, stream.ToArray());
            Assert.Equal(42, source.Single().Value);
            Assert.Equal(42, source.Single().Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ConfigurationIsImmediateAndDoesNotClaimTheStream()
    {
        using var stream = Create(SingleColumn("42"));
        var source = TabularFile.Xlsx<SheetInteger>(stream);
        Assert.Throws<ArgumentException>(() => source.WithWorksheet(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithHeaderRow(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithHeaderRow(1_048_577));
        Assert.Throws<ArgumentException>(() => source.WithHeaderComparison(StringComparer.CurrentCulture));
        Assert.Equal(42, source.Single().Value);
    }
}

[TabularRecord] public sealed partial class SheetInteger { public int Value { get; init; } }
[TabularRecord] public sealed partial class SheetDecimal { public decimal Value { get; init; } }
[TabularRecord] public sealed partial class SheetBoolean { public bool Value { get; init; } }
[TabularRecord] public sealed partial class SheetString { public string Value { get; init; } = ""; }
[TabularRecord] public sealed partial class SheetGuid { public Guid Value { get; init; } }
[TabularRecord] public sealed partial class SheetCharacter { public char Value { get; init; } }
[TabularRecord] public sealed partial class SheetEnum { public SheetStatus Value { get; init; } }
[TabularRecord] public sealed partial class SheetDouble { public double Value { get; init; } }
[TabularRecord]
public sealed partial class SheetNativeNumbers
{
    public Int128 Signed { get; init; }
    public UInt128 Unsigned { get; init; }
    public decimal Price { get; init; }
    public Half Small { get; init; }
}
public enum SheetStatus { Ready }
[TabularRecord]
public sealed partial class SheetPerson
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public bool Active { get; init; }
    public decimal? Amount { get; init; }
}
