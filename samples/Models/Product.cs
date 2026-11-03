using System.Globalization;
using LinqToFile;

namespace SampleModels;

public readonly record struct ProductCode(int Value);

public readonly struct ProductCodeConverter : IColumnConverter<ProductCode>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out ProductCode value)
    {
        value = default;
        if (context.CellKind == TabularCellKind.Text)
        {
            if (!text.StartsWith("P-", StringComparison.Ordinal)) return false;
            text = text[2..];
        }
        else if (context.CellKind != TabularCellKind.Number) return false;

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
            return false;
        value = new ProductCode(number);
        return true;
    }

    public static bool TryFormat(ProductCode value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        charsWritten = 0;
        if (value.Value <= 0) throw new FormatException();
        if (destination.Length < 2 || !value.Value.TryFormat(destination[2..], out int count, provider: CultureInfo.InvariantCulture))
            return false;
        destination[0] = 'P';
        destination[1] = '-';
        charsWritten = count + 2;
        return true;
    }
}

[TabularRecord]
public sealed partial class Product
{
    [Column("product_code"), ColumnConverter(typeof(ProductCodeConverter))]
    public ProductCode Code { get; init; }

    public string Name { get; init; } = "";
}
