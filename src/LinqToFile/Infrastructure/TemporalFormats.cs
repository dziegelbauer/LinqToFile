using System.Globalization;
using LinqToFile.Mapping;

namespace LinqToFile.Infrastructure;

/// <summary>Configuration validation for generated temporal mappings.</summary>
public static class TemporalFormats
{
    /// <summary>Validates the DateTime text format and culture.</summary>
    /// <param name="format">The format used for text conversion.</param>
    /// <param name="culture">The conversion culture.</param>
    public static void ValidateDateTime(string format, CultureInfo culture) => Validate(format, TemporalKind.DateTime, culture);
    /// <summary>Validates the DateTimeOffset text format and culture.</summary>
    /// <param name="format">The format used for text conversion.</param>
    /// <param name="culture">The conversion culture.</param>
    public static void ValidateDateTimeOffset(string format, CultureInfo culture) => Validate(format, TemporalKind.DateTimeOffset, culture);
    /// <summary>Validates the DateOnly text format and culture.</summary>
    /// <param name="format">The format used for text conversion.</param>
    /// <param name="culture">The conversion culture.</param>
    public static void ValidateDateOnly(string format, CultureInfo culture) => Validate(format, TemporalKind.DateOnly, culture);
    /// <summary>Validates the TimeOnly text format and culture.</summary>
    /// <param name="format">The format used for text conversion.</param>
    /// <param name="culture">The conversion culture.</param>
    public static void ValidateTimeOnly(string format, CultureInfo culture) => Validate(format, TemporalKind.TimeOnly, culture);

    private static void Validate(string format, TemporalKind kind, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (TemporalFormatRules.GetError(format, kind, culture) is string error)
            throw new ArgumentException(error, nameof(culture));
    }
}
