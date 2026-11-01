using LinqToFile.Reading;

namespace LinqToFile.Formats.Xlsx;

internal sealed record XlsxOptions : TabularOptions
{
    internal string? Worksheet { get; init; }
    internal int? HeaderRow { get; init; }
}
