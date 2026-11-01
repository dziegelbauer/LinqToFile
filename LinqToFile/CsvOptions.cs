using System.Text;

namespace LinqToFile;

internal sealed record CsvOptions
{
    internal Encoding Encoding { get; init; } = new UTF8Encoding(false, true);
    internal StringComparer HeaderComparison { get; init; } = StringComparer.Ordinal;
    internal char Delimiter { get; init; } = ',';
    internal char Quote { get; init; } = '"';
    internal bool DetectBom { get; init; } = true;
    internal bool TrimFields { get; init; }
    internal bool TrimHeaders { get; init; }
    internal bool SkipBlankLines { get; init; } = true;
    internal int BufferSize { get; init; } = 4096;
    internal int? MaxFieldLength { get; init; }
    internal int? MaxRecordLength { get; init; }

    internal static void ValidateCharacter(char value, string parameter)
    {
        if (value is '\r' or '\n' or '\0' || char.IsSurrogate(value))
            throw new ArgumentOutOfRangeException(parameter, "Use a non-surrogate character other than CR, LF, or NUL.");
    }
}
