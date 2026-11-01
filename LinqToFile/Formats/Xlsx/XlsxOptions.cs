using LinqToFile.Reading;

namespace LinqToFile.Formats.Xlsx;

internal sealed record XlsxOptions : TabularOptions
{
    internal string? Worksheet { get; init; }
    internal int? HeaderRow { get; init; }
    internal string? TemporaryDirectory { get; init; }
    internal bool TemporaryFiles { get; init; } = true;
    internal long SharedStringMemoryBudget { get; init; } = 8 * 1024 * 1024;
    internal long? MaxWorkbookBytes { get; init; }
    internal long? MaxDecompressedBytes { get; init; }
    internal int? MaxCellTextLength { get; init; }
    internal long? MaxTemporaryBytes { get; init; }
}
