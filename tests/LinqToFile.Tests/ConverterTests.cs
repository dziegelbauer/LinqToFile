using System.Globalization;
using System.Text;
using LinqToFile.Infrastructure;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class ConverterTests
{
    private static CsvSource<T> Read<T>(string csv) where T : class, ITabularRecord<T> =>
        TabularFile.Csv<T>(() => new MemoryStream(Encoding.UTF8.GetBytes(csv))).WithBufferSize(1);

    [Fact]
    public async Task CustomDomainTypeUsesCultureAndTrimInSyncAndAsyncReads()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        var source = Read<ConvertedAmount>("amount;plain\n 12,50 ;7\n").WithDelimiter(';').WithCulture(culture).WithFieldTrimming(true);
        culture.NumberFormat.NumberDecimalSeparator = "!";
        Assert.Equal(12.5m, source.Single().Value.Value);
        var rows = await source.ToListAsync();
        Assert.Equal(12.5m, Assert.Single(rows).Value.Value);
        Assert.Equal(7, rows[0].Plain);
    }

    [Theory]
    [InlineData("", 42)]
    [InlineData("NULL", null)]
    [InlineData("9", 9)]
    public void ConverterOwnsEmptyAndNullableValueHandling(string text, int? expected) =>
        Assert.Equal(expected, Read<ConvertedNullable>("Value\n\"" + text + "\"\n").Single().Value);

    [Fact]
    public void TrimmingOccursBeforeTheConverterAndEmptyReferencesRemainUnderItsControl()
    {
        var source = Read<ConvertedText>("Value\n\" NULL \"\n\"\"\n");
        Assert.Equal(new[] { " NULL ", "" }, source.Select(r => r.Value));
        Assert.Equal(new string?[] { null, "" }, source.WithFieldTrimming().Select(r => r.Value));
    }

    [Fact]
    public async Task NullableValueFormattingAndCultureRoundTripThroughGeneratedWriters()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        string amountPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var nullable = TabularFile.Csv<ConvertedNullable>(path);
            await nullable.CreateAsync(new[] { new ConvertedNullable { Value = null }, new ConvertedNullable { Value = 7 } });
            Assert.Equal("Value\nNULL\n7\n", File.ReadAllText(path));
            Assert.Equal(new int?[] { null, 7 }, nullable.Select(r => r.Value));

            var amounts = TabularFile.Csv<ConvertedAmount>(amountPath).WithDelimiter(';').WithCulture(CultureInfo.GetCultureInfo("fr-FR"));
            amounts.Create(new[] { new ConvertedAmount { Value = new(12.5m), Plain = 1 } });
            Assert.Equal("amount;plain\n12,5;1\n", File.ReadAllText(amountPath));
            Assert.Equal(12.5m, amounts.Single().Value.Value);
        }
        finally { File.Delete(path); File.Delete(amountPath); }
    }

    [Fact]
    public void CustomOutputStillObservesRecordLimits()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            Assert.Throws<TabularWriteException>(() => TabularFile.Csv<ConvertedText>(path).WithMaxRecordLength(8)
                .Create(new[] { new ConvertedText { Value = "123456789" } }));
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ConverterOwnsNullableReferencesAndWritingUsesDialectAndLimits()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<ConvertedText>(path).WithDelimiter(';').WithQuote('\'');
            await file.CreateAsync(new[] { new ConvertedText { Value = null }, new ConvertedText { Value = "a;b'c\n雪" } });
            Assert.Equal("Value\nNULL\n'a;b''c\n雪'\n", await File.ReadAllTextAsync(path));
            var rows = await file.ToListAsync();
            Assert.Null(rows[0].Value);
            Assert.Equal("a;b'c\n雪", rows[1].Value);
            await file.AppendAsync(new ConvertedText { Value = "tail" });
            await file.InsertAsync(1, new ConvertedText { Value = "middle" });
            Assert.Equal(new string?[] { null, "middle", "a;b'c\n雪", "tail" }, file.Select(r => r.Value));
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<TabularWriteException>(() => file.WithMaxFieldLength(8).Append(new ConvertedText { Value = "123456789" }));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FormattingRetriesWithLargerReusableBufferAndRejectsInvalidCounts()
    {
        var output = new TabularValue(CultureInfo.InvariantCulture);
        string text = new('雪', 4097);
        output.SetConverted<string?, TextConverter>(text);
        Assert.Equal(text, output.Text.ToString());
        output.Reset();
        output.SetConverted<string?, TextConverter>(null);
        Assert.Equal("NULL", output.Text.ToString());
        Assert.Throws<FormatException>(() => output.SetConverted<int, InvalidCountConverter>(1));
        Assert.Throws<FormatException>(() => output.SetConverted<int, InvalidCountConverter>(-1));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("throw-secret")]
    public void ReadFailuresKeepLocationAndSuppressConverterDetails(string text)
    {
        var error = Assert.Throws<TabularConversionException>(() => Read<ConvertedAmount>("plain,amount\n1," + text + "\n").Single());
        Assert.Equal(2, error.RecordNumber);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal(2, error.ColumnIndex);
        Assert.Equal("amount", error.ColumnHeader);
        Assert.Equal("Value", error.TargetMember);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(text, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteFailuresKeepLocationAndSuppressConverterDetails()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var error = Assert.Throws<TabularWriteException>(() => TabularFile.Csv<ConvertedAmount>(path)
                .Create(new[] { new ConvertedAmount { Value = new(-1) } }));
            Assert.Equal(1, error.RecordNumber);
            Assert.Equal("amount", error.ColumnHeader);
            Assert.Equal("Value", error.TargetMember);
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("write-secret", error.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task XlsxConverterReceivesNativeKindsLexicalValuesAndWorkbookDateSystem(bool date1904)
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Number("A2", "42.50")) +
            Row(3, Text("A3", "42.50")) + Row(4, Cell("A4", "b", "1")) + Row(5, Cell("A5", "d", "2026-01-02")),
            change: p => p["xl/workbook.xml"] = p["xl/workbook.xml"].Replace("<sheets>", $"<workbookPr date1904=\"{(date1904 ? 1 : 0)}\"/><sheets>"));
        var rows = await TabularFile.Xlsx<ConvertedMetadata>(stream).WithCulture(CultureInfo.GetCultureInfo("fr-FR")).ToListAsync();
        Assert.Equal(new[] { TabularCellKind.Number, TabularCellKind.Text, TabularCellKind.Boolean, TabularCellKind.IsoDate }, rows.Select(r => r.Value.Kind));
        Assert.Equal(new[] { "42.50", "42.50", "true", "2026-01-02" }, rows.Select(r => r.Value.Text));
        Assert.All(rows, r => { Assert.Equal(date1904 ? TabularDateSystem.Excel1904 : TabularDateSystem.Excel1900, r.Value.DateSystem); Assert.Equal("fr-FR", r.Value.Culture); });
    }

    [Fact]
    public void XlsxConverterFailuresRetainWorksheetLocation()
    {
        using var stream = Create(Row(1, Text("A1", "plain"), Text("B1", "amount")) + Row(2, Number("A2", "1"), Text("B2", "throw-secret")));
        var error = Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<ConvertedAmount>(stream).Single());
        Assert.Equal("B2", error.CellReference);
        Assert.Equal("Data", error.WorksheetName);
        Assert.Equal(2, error.WorksheetRowNumber);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("throw-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StaticConverterDispatchAddsNoPerValueAllocations()
    {
        var row = new TabularRow(16, null);
        row.Append("12.5"); row.EndField();
        var output = new TabularValue(CultureInfo.InvariantCulture);
        for (int i = 0; i < 20000; i++) output.SetConverted<Amount, AmountConverter>(row.ReadConverted<Amount, AmountConverter>(0, "Value"));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) output.SetConverted<Amount, AmountConverter>(row.ReadConverted<Amount, AmountConverter>(0, "Value"));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal("12.5", output.Text.ToString());
    }
}

public readonly record struct Amount(decimal Value);
public readonly struct AmountConverter : IColumnConverter<Amount>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out Amount value)
    {
        if (text.SequenceEqual("throw-secret")) throw new InvalidOperationException("throw-secret");
        bool valid = decimal.TryParse(text, NumberStyles.Number, context.CellKind == TabularCellKind.Number ? CultureInfo.InvariantCulture : context.Culture, out var number);
        value = new(number); return valid;
    }
    public static bool TryFormat(Amount value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        if (value.Value < 0) throw new InvalidOperationException("write-secret");
        return value.Value.TryFormat(destination, out charsWritten, provider: context.Culture);
    }
}

public readonly struct NullableConverter : IColumnConverter<int?>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out int? value)
    {
        value = null;
        if (text.IsEmpty) { value = 42; return true; }
        if (text.SequenceEqual("NULL")) return true;
        if (!int.TryParse(text, context.Culture, out int number)) return false;
        value = number; return true;
    }
    public static bool TryFormat(int? value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        if (value is int number) return number.TryFormat(destination, out charsWritten, provider: context.Culture);
        charsWritten = 0; if (!"NULL".TryCopyTo(destination)) return false; charsWritten = 4; return true;
    }
}

public readonly struct TextConverter : IColumnConverter<string?>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out string? value)
    { value = text.SequenceEqual("NULL") ? null : text.ToString(); return true; }
    public static bool TryFormat(string? value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    { value ??= "NULL"; charsWritten = 0; if (!value.AsSpan().TryCopyTo(destination)) return false; charsWritten = value.Length; return true; }
}

public readonly struct InvalidCountConverter : IColumnConverter<int>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out int value) { value = 0; return true; }
    public static bool TryFormat(int value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    { charsWritten = value < 0 ? -1 : destination.Length + 1; return true; }
}

public sealed record CellMetadata(string Text, TabularCellKind Kind, TabularDateSystem DateSystem, string Culture);
public readonly struct MetadataConverter : IColumnConverter<CellMetadata>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out CellMetadata value)
    { value = new(text.ToString(), context.CellKind, context.DateSystem, context.Culture.Name); return true; }
    public static bool TryFormat(CellMetadata value, Span<char> destination, ColumnConversionContext context, out int charsWritten) => throw new NotSupportedException();
}

[TabularRecord] public sealed partial class ConvertedAmount
{
    [Column("amount"), ColumnConverter(typeof(AmountConverter))] public Amount Value { get; init; }
    [Column("plain")] public int Plain { get; init; }
}
[TabularRecord] public sealed partial class ConvertedNullable
{ [ColumnConverter(typeof(NullableConverter))] public int? Value { get; set; } }
[TabularRecord] public sealed partial class ConvertedText
{ [ColumnConverter(typeof(TextConverter))] public string? Value { get; set; } }
[TabularRecord] public sealed partial class ConvertedMetadata
{ [ColumnConverter(typeof(MetadataConverter))] public required CellMetadata Value { get; init; } }
