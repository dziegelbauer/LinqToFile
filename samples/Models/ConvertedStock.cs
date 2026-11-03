using System.Globalization;
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class ConvertedStock
{
    [ColumnConverter(typeof(YesNoConverter))] public bool Available { get; init; }
    [ColumnConverter(typeof(OptionalCountConverter))] public int? Count { get; init; }
}

// Explicit static implementations and a private constructor also work under Native AOT.
public sealed class YesNoConverter : IColumnConverter<bool>
{
    private YesNoConverter() { }
    static bool IColumnConverter<bool>.TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out bool value)
    {
        value = text.SequenceEqual("yes");
        return value || text.SequenceEqual("no");
    }
    static bool IColumnConverter<bool>.TryFormat(bool value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        ReadOnlySpan<char> text = value ? "yes" : "no";
        charsWritten = 0;
        if (!text.TryCopyTo(destination)) return false;
        charsWritten = text.Length;
        return true;
    }
}

public readonly struct OptionalCountConverter : IColumnConverter<int?>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out int? value)
    {
        value = null;
        if (text.SequenceEqual("NULL")) return true;
        if (!int.TryParse(text, NumberStyles.Integer, context.Culture, out int number)) return false;
        value = number;
        return true;
    }
    public static bool TryFormat(int? value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        if (value is int number) return number.TryFormat(destination, out charsWritten, provider: context.Culture);
        charsWritten = 0;
        if (!"NULL".AsSpan().TryCopyTo(destination)) return false;
        charsWritten = 4;
        return true;
    }
}
