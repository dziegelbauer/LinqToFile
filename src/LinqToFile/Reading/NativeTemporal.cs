using System.Globalization;

namespace LinqToFile.Reading;

internal readonly record struct IsoCellDate(DateTime DateTime, TimeSpan? Offset, bool DateOnly);
internal enum NumericDateSystem : byte { None, Excel1900, Excel1904 }

internal static class NativeTemporal
{
    internal static bool TryCalendar(ReadOnlySpan<char> text, bool date1904, bool dateOnly, out DateTime value)
    {
        value = default;
        long epoch = date1904 ? new DateTime(1904, 1, 1).Ticks : new DateTime(1899, 12, 31).Ticks;
        long maximum = DateTime.MaxValue.Ticks - epoch + (date1904 ? 0 : TimeSpan.TicksPerDay);
        if (!ExactDecimal.TryParse(text, out var number) || number.Negative || !date1904 && number.IsDaySixty ||
            dateOnly && !number.IsIntegral || !number.TryTicks(out long serialTicks, 0, maximum)) return false;
        if (!date1904 && serialTicks / TimeSpan.TicksPerDay == 60) return false;
        if (!date1904 && serialTicks / TimeSpan.TicksPerDay > 60) serialTicks -= TimeSpan.TicksPerDay;
        if (serialTicks < 0 || serialTicks > DateTime.MaxValue.Ticks - epoch) return false;
        value = new DateTime(epoch + serialTicks, DateTimeKind.Unspecified);
        return true;
    }

    internal static bool TryTime(ReadOnlySpan<char> text, out TimeOnly value)
    {
        value = default;
        if (!ExactDecimal.TryParse(text, out var number) || number.Negative || !number.IsLessThanOne ||
            !number.TryTicks(out long ticks) || ticks >= TimeSpan.TicksPerDay) return false;
        value = new TimeOnly(ticks);
        return true;
    }

    internal static bool TryDuration(ReadOnlySpan<char> text, out TimeSpan value)
    {
        value = default;
        if (!ExactDecimal.TryParse(text, out var number) || !number.TryTicks(out long ticks)) return false;
        value = new TimeSpan(ticks);
        return true;
    }

    internal static bool TryIso(ReadOnlySpan<char> text, out IsoCellDate value, bool validateOnly = false)
    {
        value = default;
        if (text.Length < 10 || text[4] != '-' || text[7] != '-' || !Digits(text[..4], out int year) ||
            !Digits(text.Slice(5, 2), out int month) || !Digits(text.Slice(8, 2), out int day) ||
            year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        if (text.Length == 10) { value = new(date, null, true); return true; }
        if (text.Length < 19 || text[10] != 'T' || text[13] != ':' || text[16] != ':' ||
            !Digits(text.Slice(11, 2), out int hour) || !Digits(text.Slice(14, 2), out int minute) ||
            !Digits(text.Slice(17, 2), out int second) || hour > 23 || minute > 59 || second > 59) return false;
        long ticks = date.Ticks + hour * TimeSpan.TicksPerHour + minute * TimeSpan.TicksPerMinute + second * TimeSpan.TicksPerSecond;
        int index = 19;
        if (index < text.Length && text[index] == '.')
        {
            int start = ++index;
            int fraction = 0, digits = 0, firstDiscarded = -1;
            bool tail = false;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                int digit = text[index++] - '0';
                if (digits < 7) fraction = fraction * 10 + digit;
                else if (digits == 7) firstDiscarded = digit;
                else tail |= digit != 0;
                digits++;
            }
            if (index == start) return false;
            for (int i = digits; i < 7; i++) fraction *= 10;
            if (firstDiscarded > 5 || firstDiscarded == 5 && (tail || (fraction & 1) != 0)) fraction++;
            ticks += fraction;
        }
        TimeSpan? offset = null;
        if (index < text.Length)
        {
            if (text[index] == 'Z') { offset = TimeSpan.Zero; index++; }
            else
            {
                if (text.Length - index != 6 || text[index] is not ('+' or '-') || text[index + 3] != ':' ||
                    !Digits(text.Slice(index + 1, 2), out int hours) || !Digits(text.Slice(index + 4, 2), out int minutes) ||
                    hours > 14 || minutes > 59 || hours == 14 && minutes != 0) return false;
                offset = new TimeSpan(hours, minutes, 0);
                if (text[index] == '-') offset = -offset;
                index += 6;
            }
        }
        if (index != text.Length) return false;
        if (validateOnly) return true;
        if (ticks > DateTime.MaxValue.Ticks || offset is TimeSpan zone && (ticks - zone.Ticks < 0 || ticks - zone.Ticks > DateTime.MaxValue.Ticks)) return false;
        value = new(new DateTime(ticks, DateTimeKind.Unspecified), offset, false);
        return true;
    }

    private static bool Digits(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        foreach (char c in text) { if (!char.IsAsciiDigit(c)) return false; value = value * 10 + c - '0'; }
        return true;
    }
}
