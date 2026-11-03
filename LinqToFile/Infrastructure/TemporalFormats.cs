using System.Globalization;
using LinqToFile.Mapping;

namespace LinqToFile.Infrastructure;

/// <summary>Configuration validation for generated temporal mappings.</summary>
public static class TemporalFormats
{
    public static void ValidateDateTime(string format, CultureInfo culture) => Validate(format, TemporalKind.DateTime, culture);
    public static void ValidateDateTimeOffset(string format, CultureInfo culture) => Validate(format, TemporalKind.DateTimeOffset, culture);
    public static void ValidateDateOnly(string format, CultureInfo culture) => Validate(format, TemporalKind.DateOnly, culture);
    public static void ValidateTimeOnly(string format, CultureInfo culture) => Validate(format, TemporalKind.TimeOnly, culture);

    private static void Validate(string format, TemporalKind kind, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (TemporalFormatRules.GetError(format, kind, culture) is string error)
            throw new ArgumentException(error, nameof(culture));
    }
}
