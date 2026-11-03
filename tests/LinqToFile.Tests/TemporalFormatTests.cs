using System.Globalization;
using System.Text;
using LinqToFile.Infrastructure;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class TemporalFormatTests
{
    private static CsvSource<T> Read<T>(string text) where T : class, ITabularRecord<T> =>
        TabularFile.Csv<T>(() => new MemoryStream(Encoding.UTF8.GetBytes("Value\n\"" + text.Replace("\"", "\"\"") + "\"\n"))).WithBufferSize(1);

    [Fact]
    public async Task ExactCultureParsingOwnsNoWhitespaceOrGeneralParsingFallback()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        var source = Read<FormattedDate>("23 novembre 2004").WithCulture(culture);
        culture.DateTimeFormat.MonthNames = Enumerable.Repeat("changed", 13).ToArray();
        Assert.Equal(new DateOnly(2004, 11, 23), source.Single().Value);
        Assert.Equal(new DateOnly(2004, 11, 23), (await source.ToListAsync())[0].Value);
        Assert.Throws<TabularConversionException>(() => Read<FormattedDate>("2004-11-23").Single());
        Assert.Throws<TabularConversionException>(() => Read<FormattedDate>(" 23 November 2004 ").Single());
        Assert.Equal(new DateOnly(2004, 11, 23), Read<FormattedDate>(" 23 November 2004 ").WithFieldTrimming().Single().Value);
    }

    [Theory]
    [InlineData("2004/11/23 13:45:56", true)]
    [InlineData("2004/11/23 13:45:56Z", false)]
    [InlineData("2004/11/23 13:45:56+05:45", false)]
    public void DateTimeRetainsUnspecifiedKindAndRejectsOffsets(string text, bool valid)
    {
        if (!valid) { Assert.Throws<TabularConversionException>(() => Read<FormattedStamp>(text).Single()); return; }
        Assert.Equal(new DateTime(2004, 11, 23, 13, 45, 56, DateTimeKind.Unspecified), Read<FormattedStamp>(text).Single().Value);
    }

    [Theory]
    [InlineData("2004/11/23 13:45:56+05:45", 345)]
    [InlineData("2004/11/23 13:45:56-07:00", -420)]
    [InlineData("2004/11/23 13:45:56Z", 0)]
    public void DateTimeOffsetRequiresAndPreservesExplicitOffset(string text, int minutes)
    {
        var value = Read<FormattedOffset>(text).Single().Value;
        Assert.Equal(TimeSpan.FromMinutes(minutes), value.Offset);
        Assert.Equal(13, value.Hour);
        Assert.Throws<TabularConversionException>(() => Read<FormattedOffset>("2004/11/23 13:45:56").Single());
    }

    [Fact]
    public void NullableAndEmptyRulesRemainUnchanged()
    {
        Assert.Null(Read<FormattedNullableDate>("").Single().Value);
        Assert.Null(Read<FormattedNullableDate>("   ").WithFieldTrimming().Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<FormattedNullableDate>("   ").Single());
        Assert.Throws<TabularConversionException>(() => Read<FormattedDate>("").Single());
    }

    [Theory]
    [InlineData("2004-11-23")]
    [InlineData("not-a-date-private-token")]
    [InlineData("secret-invalid-date")]
    public void ConversionErrorsKeepLocationsAndOmitValues(string text)
    {
        var error = Assert.Throws<TabularConversionException>(() => Read<FormattedDate>(text).Single());
        Assert.Equal(2, error.RecordNumber);
        Assert.Equal(1, error.ColumnIndex);
        Assert.Equal("Value", error.TargetMember);
        Assert.DoesNotContain(text, error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task NativeXlsxDateCellsKeepTheirExistingRulesWhileTextUsesExactFormat()
    {
        using var stream = Create(Row(1, Text("A1", "Value")) + Row(2, Number("A2", "38314")) +
            Row(3, Cell("A3", "d", "2004-11-23")) + Row(4, Text("A4", "23 November 2004")));
        var rows = await TabularFile.Xlsx<FormattedDate>(stream).ToListAsync();
        Assert.All(rows, r => Assert.Equal(new DateOnly(2004, 11, 23), r.Value));
        using var invalid = Create(SingleColumn("2004-11-23", "str"));
        var error = Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<FormattedDate>(invalid).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.Equal("Data", error.WorksheetName);
    }

    [Fact]
    public void CulturePatternsCannotSupplyIncompleteDatesOrOffsets()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = "MM/dd";
        Assert.Throws<ArgumentException>(() => Read<FormattedStandardDate>("11/23").WithCulture(culture));
        using var stream = Create(SingleColumn("11/23", "str"));
        Assert.Throws<ArgumentException>(() => TabularFile.Xlsx<FormattedStandardDate>(stream).WithCulture(culture));
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd zzz";
        Assert.Throws<ArgumentException>(() => Read<FormattedStandardDate>("unused").WithCulture(culture));
    }

    [Fact]
    public void RoundTripStandardsKeepTheirIntrinsicInvariantCultureAndTimezoneRules()
    {
        var culture = CultureInfo.GetCultureInfo("ar-SA");
        Assert.Equal(new DateOnly(2004, 11, 23), Read<FormattedRoundTripDate>("2004-11-23").WithCulture(culture).Single().Value);
        Assert.Equal(new DateTime(2004, 11, 23, 13, 45, 56).AddTicks(1234567), Read<FormattedRoundTripStamp>("2004-11-23T13:45:56.1234567").WithCulture(culture).Single().Value);
        var offset = Read<FormattedRoundTripOffset>("2004-11-23T13:45:56.1234567+05:45").WithCulture(culture).Single().Value;
        Assert.Equal(TimeSpan.FromMinutes(345), offset.Offset);
        Assert.Equal(1234567, offset.Ticks % TimeSpan.TicksPerSecond);
        Assert.Throws<TabularConversionException>(() => Read<FormattedRoundTripStamp>("2004-11-23T13:45:56.1234567Z").Single());
        Assert.Throws<TabularConversionException>(() => Read<FormattedRoundTripOffset>("2004-11-23T13:45:56.1234567").Single());
    }

    [Theory]
    [InlineData(false, "38314.5")]
    [InlineData(true, "36852.5")]
    public void NativeDateSystemsOffsetsAndTimeFractionsIgnoreTheTextFormat(bool date1904, string serial)
    {
        using var stamp = Create(SingleColumn(serial), change: p => p["xl/workbook.xml"] = p["xl/workbook.xml"].Replace("<sheets>", $"<workbookPr date1904=\"{(date1904 ? 1 : 0)}\"/><sheets>"));
        Assert.Equal(new DateTime(2004, 11, 23, 12, 0, 0), TabularFile.Xlsx<FormattedStamp>(stamp).Single().Value);
        using var offset = Create(SingleColumn("2004-11-23T13:45:56+05:45", "d"));
        Assert.Equal(TimeSpan.FromMinutes(345), TabularFile.Xlsx<FormattedOffset>(offset).Single().Value.Offset);
        using var time = Create(SingleColumn("0.5"));
        Assert.Equal(new TimeOnly(12, 0), TabularFile.Xlsx<FormattedTime>(time).Single().Value);
    }

    [Fact]
    public void InvalidDateTimeKindsFailBeforeStagedWritesChangeExistingBytes()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<FormattedStamp>(path);
            file.Create(new[] { new FormattedStamp { Value = new(2004, 11, 23, 13, 45, 56) } });
            byte[] before = File.ReadAllBytes(path);
            var error = Assert.Throws<TabularWriteException>(() => file.Append(new FormattedStamp { Value = new(2004, 11, 23, 13, 45, 56, DateTimeKind.Utc) }));
            Assert.Equal("Value", error.TargetMember);
            Assert.Null(error.InnerException);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CsvWritesUseSelectedCultureAndPrecisionInAllMutationPaths()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<FormattedDate>(path).WithCulture(CultureInfo.GetCultureInfo("fr-FR"));
            await file.CreateAsync(new[] { new FormattedDate { Value = new(2004, 11, 23) } });
            file.Append(new FormattedDate { Value = new(2004, 11, 24) });
            await file.InsertAsync(1, new FormattedDate { Value = new(2004, 11, 25) });
            Assert.Equal("Value\n23 novembre 2004\n25 novembre 2004\n24 novembre 2004\n", File.ReadAllText(path));
            Assert.Equal(new[] { 23, 25, 24 }, file.Select(r => r.Value.Day));
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<TabularWriteException>(() => file.WithMaxFieldLength(16).Append(new FormattedDate { Value = new(2004, 9, 23) }));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FormattingUsesReusableStorageAndPermitsOmittedPrecision()
    {
        var output = new TabularValue(CultureInfo.InvariantCulture);
        output.Set(new TimeOnly(14, 30, 59).Add(TimeSpan.FromTicks(1234567)), "HH:mm");
        Assert.Equal("14:30", output.Text.ToString());
        Assert.Equal(new TimeOnly(14, 30), Read<FormattedTime>("14:30").Single().Value);
        Assert.Throws<FormatException>(() => output.Set(DateTime.UtcNow, "yyyy-MM-dd"));
        output.Set(new DateTimeOffset(2004, 11, 23, 13, 45, 56, TimeSpan.FromMinutes(345)), "yyyy-MM-dd HH:mm:sszzz");
        Assert.Equal("2004-11-23 13:45:56+05:45", output.Text.ToString());
        output.Set(new DateOnly(2004, 11, 23), "yyyy-MM-dd'" + new string('x', 1024) + "'");
        Assert.Equal(1034, output.Text.Length);
    }

    [Fact]
    public void FormattedLiteralsUseNormalCsvEscapingAndRetainMultilineUnicode()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<FormattedLiteralDate>(path).WithBufferSize(1);
            file.Create(new[] { new FormattedLiteralDate { Value = new(2004, 11, 23) } });
            Assert.Equal("Value\n\"2004-11-23, \"\"雪\"\"\nend\"\n", File.ReadAllText(path));
            Assert.Equal(new DateOnly(2004, 11, 23), file.Single().Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExactNumericPatternsAddNoPerFieldAllocationsAfterWarmup()
    {
        var row = new TabularRow(32, null);
        row.Append("2004/11/23 13:45:56"); row.EndField();
        var output = new TabularValue(CultureInfo.InvariantCulture);
        const string format = "yyyy/MM/dd HH:mm:ss";
        for (int i = 0; i < 20000; i++) output.Set(row.ReadDateTimeExact(0, "Value", format), format);
        long bytes = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) output.Set(row.ReadDateTimeExact(0, "Value", format), format);
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, bytes);
    }
}

[TabularRecord] public sealed partial class FormattedDate
{ [TemporalFormat("dd MMMM yyyy")] public DateOnly Value { get; init; } }
[TabularRecord] public sealed partial class FormattedNullableDate
{ [TemporalFormat("dd MMMM yyyy")] public DateOnly? Value { get; init; } }
[TabularRecord] public sealed partial class FormattedStamp
{ [TemporalFormat("yyyy/MM/dd HH:mm:ssK")] public DateTime Value { get; init; } }
[TabularRecord] public sealed partial class FormattedOffset
{ [TemporalFormat("yyyy/MM/dd HH:mm:ssK")] public DateTimeOffset Value { get; init; } }
[TabularRecord] public sealed partial class FormattedTime
{ [TemporalFormat("HH:mm")] public TimeOnly Value { get; init; } }
[TabularRecord] public sealed partial class FormattedStandardDate
{ [TemporalFormat("d")] public DateOnly Value { get; init; } }
[TabularRecord] public sealed partial class FormattedRoundTripDate
{ [TemporalFormat("O")] public DateOnly Value { get; init; } }
[TabularRecord] public sealed partial class FormattedRoundTripStamp
{ [TemporalFormat("O")] public DateTime Value { get; init; } }
[TabularRecord] public sealed partial class FormattedRoundTripOffset
{ [TemporalFormat("O")] public DateTimeOffset Value { get; init; } }
[TabularRecord] public sealed partial class FormattedLiteralDate
{ [TemporalFormat("yyyy-MM-dd', \"雪\"\nend'")] public DateOnly Value { get; init; } }
