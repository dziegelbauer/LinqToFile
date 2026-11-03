using System.Globalization;

namespace LinqToFile.Infrastructure;

public sealed partial class TabularRow
{
    private bool IsNative(int ordinal) => kinds is not null && kinds[ordinal] != CellKind.Text;

    public DateTime ReadDateTimeExact(int ordinal, string member, string format)
    {
        if (IsNative(ordinal)) return ReadDateTime(ordinal, member);
        var text = GetField(ordinal);
        try
        {
            if (text.IndexOf('\0') < 0 && DateTime.TryParseExact(text, format, Culture,
                    DateTimeStyles.AdjustToUniversal, out var value) && value.Kind == DateTimeKind.Unspecified) return value;
        }
        catch (FormatException) { }
        throw CreateConversionException(ordinal, member);
    }

    public DateTimeOffset ReadDateTimeOffsetExact(int ordinal, string member, string format)
    {
        if (IsNative(ordinal)) return ReadDateTimeOffset(ordinal, member);
        var text = GetField(ordinal);
        try
        {
            // K can match an absent offset. Require the BCL's explicit-zone marker as well.
            if (text.IndexOf('\0') < 0 && DateTime.TryParseExact(text, format, Culture,
                    DateTimeStyles.AdjustToUniversal, out var marker) && marker.Kind == DateTimeKind.Utc &&
                DateTimeOffset.TryParseExact(text, format, Culture, DateTimeStyles.None, out var value)) return value;
        }
        catch (FormatException) { }
        throw CreateConversionException(ordinal, member);
    }

    public DateOnly ReadDateOnlyExact(int ordinal, string member, string format)
    {
        if (IsNative(ordinal)) return ReadDateOnly(ordinal, member);
        var text = GetField(ordinal);
        try
        {
            if (text.IndexOf('\0') < 0 && DateOnly.TryParseExact(text, format, Culture, DateTimeStyles.None, out var value)) return value;
        }
        catch (FormatException) { }
        throw CreateConversionException(ordinal, member);
    }

    public TimeOnly ReadTimeOnlyExact(int ordinal, string member, string format)
    {
        if (IsNative(ordinal)) return ReadTimeOnly(ordinal, member);
        var text = GetField(ordinal);
        try
        {
            if (text.IndexOf('\0') < 0 && TimeOnly.TryParseExact(text, format, Culture, DateTimeStyles.None, out var value)) return value;
        }
        catch (FormatException) { }
        throw CreateConversionException(ordinal, member);
    }
}

public sealed partial class TabularValue
{
    public void Set(DateTime value, string format)
    {
        if (value.Kind != DateTimeKind.Unspecified) throw new FormatException();
        Format(value, format, culture);
    }
    public void Set(DateTimeOffset value, string format) => Format(value, format, culture);
    public void Set(DateOnly value, string format) => Format(value, format, culture);
    public void Set(TimeOnly value, string format) => Format(value, format, culture);
}
