using System.Globalization;

namespace LinqToFile.Formats.Xlsx;

internal readonly record struct CellAddress(int Column, int Row)
{
    internal static CellAddress Parse(ReadOnlySpan<char> value)
    {
        int index = 0;
        int column = 0;
        while (index < value.Length && value[index] is >= 'A' and <= 'Z')
        {
            if (index == 3) throw new XlsxFormatException("A cell coordinate is invalid.");
            column = column * 26 + value[index++] - 'A' + 1;
        }
        if (column is < 1 or > 16384 || index == value.Length || value[index] == '0' ||
            !int.TryParse(value[index..], NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row is < 1 or > 1_048_576)
            throw new XlsxFormatException("A cell coordinate is invalid.");
        return new(column, row);
    }
}
