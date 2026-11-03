using System.Globalization;

namespace LinqToFile.Formats.Xlsx;

internal sealed record XlsxWriteOptions
{
    internal string Worksheet { get; init; } = "Sheet1";
    internal CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    internal StringComparer HeaderComparison { get; init; } = StringComparer.Ordinal;
    internal bool TrimFields { get; init; }
    internal bool TrimHeaders { get; init; }
    internal TabularDateSystem DateSystem { get; init; } = TabularDateSystem.Excel1900;
    internal ExistingFileBehavior ExistingFileBehavior { get; init; }
    internal int? MaxCellTextLength { get; init; }
}
