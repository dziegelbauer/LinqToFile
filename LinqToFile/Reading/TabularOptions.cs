using System.Globalization;

namespace LinqToFile.Reading;

internal abstract record TabularOptions
{
    internal StringComparer HeaderComparison { get; init; } = StringComparer.Ordinal;
    internal CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    internal bool TrimFields { get; init; }
    internal bool TrimHeaders { get; init; }
}
