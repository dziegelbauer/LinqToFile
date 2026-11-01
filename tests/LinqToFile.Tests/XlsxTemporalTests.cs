using System.Globalization;
using System.Numerics;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxTemporalTests
{
    [Theory]
    [InlineData(false, "0", 1899, 12, 31)]
    [InlineData(false, "1", 1900, 1, 1)]
    [InlineData(false, "59", 1900, 2, 28)]
    [InlineData(false, "61", 1900, 3, 1)]
    [InlineData(false, "2958465", 9999, 12, 31)]
    [InlineData(true, "0", 1904, 1, 1)]
    [InlineData(true, "59", 1904, 2, 29)]
    [InlineData(true, "60", 1904, 3, 1)]
    [InlineData(true, "2957003", 9999, 12, 31)]
    public void CalendarSerialsHonorWorkbookDateSystem(bool date1904, string serial, int year, int month, int day)
    {
        using var stream = Calendar(serial, date1904);
        var value = TabularFile.Xlsx<SheetDateTime>(stream).WithCulture(CultureInfo.GetCultureInfo("fr-FR")).Single().Value;
        Assert.Equal(new DateTime(year, month, day), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-1e-1000000000")]
    [InlineData("60")]
    [InlineData("60.5")]
    [InlineData("60.9999999999999999999999999999")]
    [InlineData("59.9999999999999999999999999999")]
    [InlineData("2958466")]
    [InlineData("1e999999999999")]
    public void InvalidSerialsDoNotWrapClampOrSubstitute(string serial)
    {
        using var stream = Calendar(serial);
        Assert.Equal("A2", Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateTime>(stream).Single()).CellReference);
    }

    [Fact]
    public void DateOnlyRequiresIntegralDaysAndSerialsHaveNoOffsets()
    {
        using var whole = Calendar("61.000");
        using var fraction = Calendar("61.00000000000000000000000000001");
        using var offset = Calendar("61");
        Assert.Equal(new DateOnly(1900, 3, 1), TabularFile.Xlsx<SheetDateOnly>(whole).Single().Value);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateOnly>(fraction).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetOffset>(offset).Single());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.5", 432000000000)]
    [InlineData("1e-100000000000", 0)]
    public void TimeOnlyUsesOnlyFractionalDays(string serial, long ticks)
    {
        using var stream = Calendar(serial);
        Assert.Equal(ticks, TabularFile.Xlsx<SheetTimeOnly>(stream).Single().Value.Ticks);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("-0.1")]
    [InlineData("0.999999999999999999999999999999999")]
    public void TimeOnlyRejectsWholeDaysAndRoundingToNextDay(string serial)
    {
        using var stream = Calendar(serial);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetTimeOnly>(stream).Single());
    }

    [Theory]
    [InlineData("-1.5", -1296000000000)]
    [InlineData("60.5", 52272000000000)]
    [InlineData("0.000000000015625", 14)]
    [InlineData("0.000000000046875", 40)]
    [InlineData("0.0000000000468750000000000000000000000001", 41)]
    [InlineData("0.0000000000468749999999999999999999999999", 40)]
    [InlineData("-0.000000000015625", -14)]
    [InlineData("-0.000000000046875", -40)]
    [InlineData("0e999999999999999", 0)]
    [InlineData("-1e-999999999999999", 0)]
    public void DurationsUseExactTickRoundingWithoutCalendarRules(string serial, long expectedTicks)
    {
        using var stream = Calendar(serial);
        Assert.Equal(expectedTicks, TabularFile.Xlsx<SheetDuration>(stream).Single().Value.Ticks);
    }

    [Theory]
    [InlineData("1e8")]
    [InlineData("-1e8")]
    public void DurationOverflowThrows(string serial)
    {
        using var stream = Calendar(serial);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDuration>(stream).Single());
    }

    [Fact]
    public void TemporalRangeChecksUseTheOriginalValueBeforeRounding()
    {
        string Below(BigInteger ticks) => DecimalDays(ticks, false);
        string Above(BigInteger ticks) => DecimalDays(ticks, true);
        using var max = Calendar(Below(long.MaxValue));
        using var over = Calendar(Above(long.MaxValue));
        using var min = Calendar("-" + Below(-(BigInteger)long.MinValue));
        using var under = Calendar("-" + Above(-(BigInteger)long.MinValue));
        Assert.Equal(TimeSpan.MaxValue, TabularFile.Xlsx<SheetDuration>(max).Single().Value);
        Assert.Equal(TimeSpan.MinValue, TabularFile.Xlsx<SheetDuration>(min).Single().Value);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDuration>(over).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDuration>(under).Single());
        long serialMaximum = DateTime.MaxValue.Ticks - new DateTime(1899, 12, 31).Ticks + TimeSpan.TicksPerDay;
        using var calendarMax = Calendar(Below(serialMaximum));
        using var calendarOver = Calendar(Above(serialMaximum));
        Assert.Equal(DateTime.MaxValue, TabularFile.Xlsx<SheetDateTime>(calendarMax).Single().Value);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateTime>(calendarOver).Single());
    }

    private static string DecimalDays(BigInteger ticks, bool above)
    {
        var numerator = ticks * BigInteger.Pow(10, 30) / TimeSpan.TicksPerDay;
        if (above) numerator++;
        string digits = numerator.ToString(CultureInfo.InvariantCulture).PadLeft(31, '0');
        return digits[..^30] + "." + digits[^30..];
    }

    [Theory]
    [InlineData("2026-01-02", "2026-01-02T00:00:00.0000000")]
    [InlineData("2026-01-02T03:04:05.12345665", "2026-01-02T03:04:05.1234566")]
    [InlineData("2026-01-02T03:04:05.12345675", "2026-01-02T03:04:05.1234568")]
    [InlineData("2026-01-02T23:59:59.99999999", "2026-01-03T00:00:00.0000000")]
    public void NativeIsoDateTimesRoundOnceWithoutLocalTime(string iso, string expected)
    {
        using var stream = Create(SingleColumn(iso, "d"));
        var actual = TabularFile.Xlsx<SheetDateTime>(stream).Single().Value;
        Assert.Equal(expected, actual.ToString("O", CultureInfo.InvariantCulture));
        Assert.Equal(DateTimeKind.Unspecified, actual.Kind);
    }

    [Theory]
    [InlineData("2026-01-02T03:04:05-07:00", -7)]
    [InlineData("2026-01-02T03:04:05Z", 0)]
    [InlineData("2026-01-02T03:04:05+14:00", 14)]
    public void IsoOffsetsArePreservedAndRejectedForDateTime(string iso, int hours)
    {
        using var stream = Create(SingleColumn(iso, "d"));
        Assert.Equal(TimeSpan.FromHours(hours), TabularFile.Xlsx<SheetOffset>(stream).Single().Value.Offset);
        using var noOffsetTarget = Create(SingleColumn(iso, "d"));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateTime>(noOffsetTarget).Single());
    }

    [Fact]
    public void IsoDateOnlyAndTypeBoundariesAreStrict()
    {
        using var a = Create(SingleColumn("2026-01-02", "d"));
        using var b = Create(SingleColumn("2026-01-02T00:00:00", "d"));
        using var c = Create(SingleColumn("2026-01-02", "d"));
        using var d = Create(SingleColumn("2026-01-02", "d"));
        using var e = Create(SingleColumn("2026-01-02", "d"));
        Assert.Equal(new DateOnly(2026, 1, 2), TabularFile.Xlsx<SheetDateOnly>(a).Single().Value);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateOnly>(b).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetTimeOnly>(c).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDuration>(d).Single());
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetOffset>(e).Single());
    }

    [Theory]
    [InlineData("1900-02-29")]
    [InlineData("2026-01-02T24:00:00")]
    [InlineData("2026-01-02T23:59:60")]
    [InlineData("2026-01-02T03:04:05+14:01")]
    [InlineData("2026-01-02T03:04:05.")]
    [InlineData("2026-01-02T03:04")]
    [InlineData("2026-01-02T03:04:05z")]
    public void MalformedIsoCellsFailEvenForStringTargets(string iso)
    {
        using var stream = Create(SingleColumn(iso, "d"));
        Assert.Equal("A2", Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetString>(stream).Single()).CellReference);
    }

    [Fact]
    public void ValidIsoStringsKeepStoredPrecisionEvenWhenTypedRoundingOverflows()
    {
        const string iso = "9999-12-31T23:59:59.99999999999999999";
        using var text = Create(SingleColumn(iso, "d"));
        using var date = Create(SingleColumn(iso, "d"));
        Assert.Equal(iso, TabularFile.Xlsx<SheetString>(text).Single().Value);
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<SheetDateTime>(date).Single());
    }

    private static MemoryStream Calendar(string serial, bool date1904 = false) => Create(SingleColumn(serial), change: p =>
        p["xl/workbook.xml"] = p["xl/workbook.xml"].Replace("<sheets>", $"<workbookPr date1904=\"{(date1904 ? "1" : "0")}\"/><sheets>", StringComparison.Ordinal));
}

[TabularRecord] public sealed partial class SheetDateTime { public DateTime Value { get; init; } }
[TabularRecord] public sealed partial class SheetDateOnly { public DateOnly Value { get; init; } }
[TabularRecord] public sealed partial class SheetOffset { public DateTimeOffset Value { get; init; } }
[TabularRecord] public sealed partial class SheetTimeOnly { public TimeOnly Value { get; init; } }
[TabularRecord] public sealed partial class SheetDuration { public TimeSpan Value { get; init; } }
