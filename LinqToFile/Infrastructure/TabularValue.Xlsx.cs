using System.Globalization;

namespace LinqToFile.Infrastructure;

internal enum ExportCellKind : byte { Blank, Text, Number, Boolean, Date, DateTime, Time }

public sealed partial class TabularValue
{
    private static void ValidateExcelNumber(ReadOnlySpan<char> value)
    {
        int exponent = value.IndexOfAny('e', 'E');
        var mantissa = exponent < 0 ? value : value[..exponent];
        int digits = 0, trailing = 0;
        bool started = false;
        foreach (char c in mantissa)
        {
            if (c is < '0' or > '9') continue;
            if (!started && c == '0') continue;
            started = true;
            digits++;
            trailing = c == '0' ? trailing + 1 : 0;
        }
        if (digits - trailing > 15 || !double.TryParse(value, CultureInfo.InvariantCulture, out double number) ||
            !double.IsFinite(number) || Math.Abs(number) > 9.99999999999999E307 ||
            (number != 0 && Math.Abs(number) < 2.2250738585072014E-308) || (started && number == 0))
            throw new FormatException();
    }

    private void SetCalendar(long ticks, ExportCellKind kind)
    {
        long origin = dateSystem == TabularDateSystem.Excel1904
            ? new DateTime(1904, 1, 1).Ticks : new DateTime(1899, 12, 31).Ticks;
        long minimum = dateSystem == TabularDateSystem.Excel1904 ? origin : new DateTime(1900, 1, 1).Ticks;
        if (ticks < minimum) throw new FormatException();
        long relative = ticks - origin;
        if (dateSystem == TabularDateSystem.Excel1900 && ticks >= new DateTime(1900, 3, 1).Ticks)
            relative += TimeSpan.TicksPerDay;
        SetSerial(relative, kind);
    }

    private void SetSerial(long ticks, ExportCellKind kind)
    {
        decimal days = (decimal)ticks / TimeSpan.TicksPerDay;
        int exponent = 0;
        decimal magnitude = days;
        if (magnitude != 0)
        {
            while (magnitude >= 10) { magnitude /= 10; exponent++; }
            while (magnitude < 1) { magnitude *= 10; exponent--; }
        }
        decimal candidate = Math.Round(days, Math.Clamp(14 - exponent, 0, 28), MidpointRounding.ToEven);
        if (Math.Round(candidate * TimeSpan.TicksPerDay, 0, MidpointRounding.ToEven) != ticks)
            throw new FormatException();
        Format(candidate, "G29", CultureInfo.InvariantCulture);
        ValidateExcelNumber(Text);
        OutputKind = kind;
    }
}
