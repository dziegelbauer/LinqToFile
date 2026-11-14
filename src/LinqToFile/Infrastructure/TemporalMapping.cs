using System.Globalization;

namespace LinqToFile.Infrastructure;

public sealed partial class TabularRow
{
    private bool IsNative(int ordinal) => kinds is not null && kinds[ordinal] != CellKind.Text;

    /// <summary>Reads text as DateTime with the exact format; native XLSX cells use native conversion.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="member">The target property name used in error diagnostics.</param>
    /// <param name="format">The format used for text conversion.</param>
    /// <returns>The converted field value.</returns>
    /// <exception cref="TabularConversionException">The field is not valid for the requested type or format.</exception>
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

    /// <summary>Reads text as DateTimeOffset with the exact format; native XLSX cells use native conversion.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="member">The target property name used in error diagnostics.</param>
    /// <param name="format">The format used for text conversion.</param>
    /// <returns>The converted field value.</returns>
    /// <exception cref="TabularConversionException">The field is not valid for the requested type or format.</exception>
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

    /// <summary>Reads text as DateOnly with the exact format; native XLSX cells use native conversion.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="member">The target property name used in error diagnostics.</param>
    /// <param name="format">The format used for text conversion.</param>
    /// <returns>The converted field value.</returns>
    /// <exception cref="TabularConversionException">The field is not valid for the requested type or format.</exception>
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

    /// <summary>Reads text as TimeOnly with the exact format; native XLSX cells use native conversion.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="member">The target property name used in error diagnostics.</param>
    /// <param name="format">The format used for text conversion.</param>
    /// <returns>The converted field value.</returns>
    /// <exception cref="TabularConversionException">The field is not valid for the requested type or format.</exception>
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
    /// <summary>Formats a temporal value as text with the exact format and configured culture.</summary>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="format">The format used for text conversion.</param>
    public void Set(DateTime value, string format)
    {
        if (value.Kind != DateTimeKind.Unspecified) throw new FormatException();
        Format(value, format, culture);
    }
    /// <summary>Formats a temporal value as text with the exact format and configured culture.</summary>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="format">The format used for text conversion.</param>
    public void Set(DateTimeOffset value, string format) => Format(value, format, culture);
    /// <summary>Formats a temporal value as text with the exact format and configured culture.</summary>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="format">The format used for text conversion.</param>
    public void Set(DateOnly value, string format) => Format(value, format, culture);
    /// <summary>Formats a temporal value as text with the exact format and configured culture.</summary>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="format">The format used for text conversion.</param>
    public void Set(TimeOnly value, string format) => Format(value, format, culture);
}
