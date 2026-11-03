using System.Globalization;

namespace LinqToFile;

/// <summary>Converts one property's complete type, including its nullable value form.</summary>
/// <typeparam name="T">The mapped property type.</typeparam>
public interface IColumnConverter<T>
{
    /// <summary>Returns false for invalid input. Empty input is passed to the converter.</summary>
    static abstract bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out T value);

    /// <summary>Returns false only if the destination is too small. Throw for an invalid value.</summary>
    /// <remarks>The library can retry this method with a larger span. Do not retain the span or depend on a call count.</remarks>
    static abstract bool TryFormat(T value, Span<char> destination, ColumnConversionContext context, out int charsWritten);
}

/// <summary>The source representation of a field. Native values retain their XLSX lexical form.</summary>
public enum TabularCellKind : byte { Text, Number, Boolean, IsoDate }

/// <summary>The workbook date system, or None for a source without one.</summary>
public enum TabularDateSystem : byte { None, Excel1900, Excel1904 }

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
