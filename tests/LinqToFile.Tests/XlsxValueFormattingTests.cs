using System.Globalization;
using LinqToFile.Infrastructure;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxValueFormattingTests
{
    [Fact]
    public void NativeValuesIgnoreTextCultureAndNativeNullTokenCollisions()
    {
        var value = new TabularValue(CultureInfo.GetCultureInfo("fr-FR"), dateSystem: TabularDateSystem.Excel1900);
        value.SetNumber(12.5m);
        Assert.Equal("12.5", value.Text.ToString());
        Assert.Equal(ExportCellKind.Number, value.OutputKind);
        value.RejectNullToken("12.5", StringComparison.Ordinal);
        value.Set(false);
        Assert.Equal("0", value.Text.ToString());
        Assert.Equal(ExportCellKind.Boolean, value.OutputKind);
        value.SetText("0");
        Assert.Throws<FormatException>(() => value.RejectNullToken("0", StringComparison.Ordinal));
        value.SetText(null);
        Assert.Equal(ExportCellKind.Blank, value.OutputKind);
    }

    [Fact]
    public void NumericPrecisionCountsSignificantDigitsAndNeverRounds()
    {
        var value = new TabularValue(CultureInfo.InvariantCulture, dateSystem: TabularDateSystem.Excel1900);
        value.SetNumber(123456789012345m);
        value.SetNumber(1000000000000000L);
        value.SetNumber(0.0000000000000000000000000001m);
        Assert.Throws<FormatException>(() => value.SetNumber(1234567890123456m));
        Assert.Throws<FormatException>(() => value.SetNumber(1d / 3, "R"));
        Assert.Throws<FormatException>(() => value.SetNumber(double.Epsilon, "R"));
        Assert.Throws<FormatException>(() => value.SetNumber(double.PositiveInfinity, "R"));
    }

    [Theory]
    [InlineData(TabularDateSystem.Excel1900, 1900, "1")]
    [InlineData(TabularDateSystem.Excel1904, 1904, "0")]
    public void CalendarOriginAndTemporalPrecisionAreStrict(TabularDateSystem system, int year, string serial)
    {
        var value = new TabularValue(CultureInfo.InvariantCulture, dateSystem: system);
        value.Set(new DateOnly(year, 1, 1));
        Assert.Equal(serial, value.Text.ToString());
        Assert.Equal(ExportCellKind.Date, value.OutputKind);
        Assert.Throws<FormatException>(() => value.Set(new DateOnly(year - 1, 12, 31)));
        var noon = new DateTime(2026, 11, 3, 12, 0, 0);
        value.Set(noon);
        Assert.Equal(ExportCellKind.DateTime, value.OutputKind);
        Assert.Throws<FormatException>(() => value.Set(noon.AddTicks(1)));
        Assert.Throws<FormatException>(() => value.Set(DateTime.SpecifyKind(noon, DateTimeKind.Utc)));
        value.Set(new TimeOnly(12, 0));
        Assert.Equal("0.5", value.Text.ToString());
        value.Set(new DateTimeOffset(noon, TimeSpan.FromHours(-7)));
        Assert.Equal(ExportCellKind.Text, value.OutputKind);
        Assert.EndsWith("-07:00", value.Text.ToString(), StringComparison.Ordinal);
        value.Set(noon, "yyyy-MM-dd");
        Assert.Equal(ExportCellKind.Text, value.OutputKind);
        Assert.Equal("2026-11-03", value.Text.ToString());
    }
}
