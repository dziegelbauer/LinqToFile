using System;
using System.Globalization;

namespace LinqToFile.Mapping;

// Shared source with the netstandard generator. Configuration checks never inspect row data.
internal enum TemporalKind { DateTime, DateTimeOffset, DateOnly, TimeOnly }

internal static class TemporalFormatRules
{
    [Flags]
    private enum Parts { None = 0, Year = 1, Month = 2, Day = 4, Date = 8, Time = 16, Offset = 32, Zone = 64, TimeValue = 128 }

    internal static string? GetError(string? format, TemporalKind kind, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(format)) return "Specify a nonempty temporal format.";
        if (format!.IndexOf('\0') >= 0) return "Temporal formats cannot contain NUL characters.";
        Parts parts;
        string? error;
        if (format!.Length == 1)
        {
            var info = culture.DateTimeFormat;
            char code = format[0];
            if (kind == TemporalKind.DateTimeOffset)
                return code == 'O' || code == 'o' ? null : "DateTimeOffset requires O/o or a custom format with zzz or K to preserve its offset.";
            if (code == 'O' || code == 'o') return null;
            if (code == 'R' || code == 'r')
                return kind == TemporalKind.DateOnly || kind == TemporalKind.TimeOnly ? null : "The format must not introduce or convert a timezone.";
            if (kind == TemporalKind.TimeOnly)
            {
                if (code != 't' && code != 'T') return "Use a supported TimeOnly format.";
                error = Analyze(code == 't' ? info.ShortTimePattern : info.LongTimePattern, out parts);
            }
            else
            {
                string date;
                string? time = null;
                switch (code)
                {
                    case 'd': date = info.ShortDatePattern; break;
                    case 'D': date = info.LongDatePattern; break;
                    case 'f': date = info.LongDatePattern; time = info.ShortTimePattern; break;
                    case 'F': date = info.LongDatePattern; time = info.LongTimePattern; break;
                    case 'g': date = info.ShortDatePattern; time = info.ShortTimePattern; break;
                    case 'G': date = info.ShortDatePattern; time = info.LongTimePattern; break;
                    case 's': return kind == TemporalKind.DateTime ? null : "Use a supported DateOnly format.";
                    default: return "Use a supported format with a complete date and no timezone conversion.";
                }
                if (kind == TemporalKind.DateOnly && time != null) return "DateOnly formats cannot contain time components.";
                error = Analyze(date, out parts);
                if (error == null && time != null)
                {
                    error = Analyze(time, out var timeParts);
                    parts |= timeParts;
                }
            }
        }
        else error = Analyze(format, out parts);
        if (error != null) return error;
        const Parts completeDate = Parts.Year | Parts.Month | Parts.Day;
        if (kind != TemporalKind.TimeOnly && (parts & completeDate) != completeDate)
            return "Specify a complete date: year, month, and day of month.";
        if (kind == TemporalKind.DateOnly && (parts & (Parts.Time | Parts.Offset | Parts.Zone)) != 0)
            return "DateOnly formats cannot contain time or timezone components.";
        if (kind == TemporalKind.TimeOnly && ((parts & (Parts.Date | Parts.Offset | Parts.Zone)) != 0 || (parts & Parts.TimeValue) == 0))
            return "TimeOnly requires time components without date or timezone components.";
        if (kind == TemporalKind.DateTime && (parts & Parts.Offset) != 0)
            return "DateTime formats cannot contain an offset. Use DateTimeOffset instead.";
        if (kind == TemporalKind.DateTimeOffset && (parts & (Parts.Offset | Parts.Zone)) == 0)
            return "DateTimeOffset formats must contain zzz or K to preserve the offset.";
        return null;
    }

    private static string? Analyze(string pattern, out Parts parts)
    {
        parts = Parts.None;
        for (int i = 0; i < pattern.Length; i++)
        {
            char token = pattern[i];
            if (token == '\\')
            {
                if (++i == pattern.Length) return "A format escape must have a following character.";
                continue;
            }
            if (token == '\'' || token == '"')
            {
                char quote = token;
                bool closed = false;
                while (++i < pattern.Length)
                {
                    if (pattern[i] == '\\') { if (++i == pattern.Length) break; }
                    else if (pattern[i] == quote) { closed = true; break; }
                }
                if (!closed) return "Close every quoted format literal.";
                continue;
            }
            int count = 1;
            if (token == '%')
            {
                if (++i == pattern.Length || pattern[i] == '%' || pattern[i] == '\\' || pattern[i] == '\'' || pattern[i] == '"')
                    return "A percent escape must precede a custom format specifier.";
                token = pattern[i];
            }
            else while (i + 1 < pattern.Length && pattern[i + 1] == token) { count++; i++; }
            switch (token)
            {
                case 'y': parts |= Parts.Year | Parts.Date; break;
                case 'M': parts |= Parts.Month | Parts.Date; break;
                case 'd': parts |= Parts.Date; if (count <= 2) parts |= Parts.Day; break;
                case 'g': case '/': parts |= Parts.Date; break;
                case ':': parts |= Parts.Time; break;
                case 'h': case 'H': case 'm': case 's': case 't': parts |= Parts.Time | Parts.TimeValue; break;
                case 'f': case 'F':
                    if (count > 7) return "Fractional seconds support at most seven digits.";
                    parts |= Parts.Time | Parts.TimeValue; break;
                case 'z':
                    if (count != 3) return "Use zzz to preserve offset hours and minutes.";
                    parts |= Parts.Offset; break;
                case 'K':
                    if (count != 1) return "Use a single K timezone specifier.";
                    parts |= Parts.Zone; break;
            }
        }
        return null;
    }
}
