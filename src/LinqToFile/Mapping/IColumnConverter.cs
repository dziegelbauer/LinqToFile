using System.Globalization;

namespace LinqToFile;

/// <summary>Converts one property's complete type, including its nullable value form.</summary>
/// <typeparam name="T">The mapped property type.</typeparam>
public interface IColumnConverter<T>
{
    /// <summary>Returns false for invalid input. Empty input is passed to the converter.</summary>
    /// <param name="text">The input text, including empty input.</param>
    /// <param name="context">The conversion culture and source cell metadata.</param>
    /// <param name="value">The parsed value on success.</param>
    /// <returns>True when parsing succeeds; false when the input is invalid.</returns>
    static abstract bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out T value);

    /// <summary>Returns false only if the destination is too small. Throw for an invalid value.</summary>
    /// <remarks>The library can retry this method with a larger span. Do not retain the span or depend on a call count.</remarks>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="destination">The output character buffer. Do not retain this span.</param>
    /// <param name="context">The conversion culture and source cell metadata.</param>
    /// <param name="charsWritten">The number of characters written on success.</param>
    /// <returns>True when formatting succeeds; false only when the destination is too small.</returns>
    static abstract bool TryFormat(T value, Span<char> destination, ColumnConversionContext context, out int charsWritten);
}

/// <summary>The source representation of a field. Native values retain their XLSX lexical form.</summary>
public enum TabularCellKind : byte
{
    /// <summary>Text content, including CSV fields.</summary>
    Text,
    /// <summary>A native XLSX numeric value.</summary>
    Number,
    /// <summary>A native XLSX boolean value.</summary>
    Boolean,
    /// <summary>A native XLSX ISO date value.</summary>
    IsoDate
}

/// <summary>The workbook date system, or None for a source without one.</summary>
public enum TabularDateSystem : byte
{
    /// <summary>No workbook date system applies.</summary>
    None,
    /// <summary>The Excel 1900 date system.</summary>
    Excel1900,
    /// <summary>The Excel 1904 date system.</summary>
    Excel1904
}

/// <summary>Immutable metadata for a converter invocation. This value does not retain row storage.</summary>
public readonly struct ColumnConversionContext
{
    internal ColumnConversionContext(CultureInfo culture, TabularCellKind cellKind, TabularDateSystem dateSystem)
    { Culture = culture; CellKind = cellKind; DateSystem = dateSystem; }

    /// <summary>The configured read-only culture snapshot. Native XLSX lexical numbers remain invariant.</summary>
    public CultureInfo Culture { get; }
    /// <summary>The source cell kind. CSV reads and writes use Text.</summary>
    public TabularCellKind CellKind { get; }
    /// <summary>The workbook date system. CSV reads and writes use None.</summary>
    public TabularDateSystem DateSystem { get; }
}
