using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

internal sealed record CsvOptions : TextOptions
{
    internal char Delimiter { get; init; } = ',';
    internal char Quote { get; init; } = '"';
    internal bool SkipBlankLines { get; init; } = true;
    internal int? MaxFieldLength { get; init; }
    internal int? MaxRecordLength { get; init; }
    internal bool InPlaceWrites { get; init; }
    internal LineEnding? NewLine { get; init; }
    internal bool ValidateAppend { get; init; } = true;
    internal ExistingFileBehavior ExistingFileBehavior { get; init; }

    internal static void ValidateCharacter(char value, string parameter)
    {
        if (value is '\r' or '\n' or '\0' || char.IsSurrogate(value))
            throw new ArgumentOutOfRangeException(parameter, "Use a non-surrogate character other than CR, LF, or NUL.");
    }
}
