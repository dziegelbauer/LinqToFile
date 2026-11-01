using System.Globalization;
using System.Text;

namespace LinqToFile.Reading;

internal abstract record TabularOptions
{
    internal Encoding Encoding { get; init; } = new UTF8Encoding(false, true);
    internal StringComparer HeaderComparison { get; init; } = StringComparer.Ordinal;
    internal CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    internal bool DetectBom { get; init; } = true;
    internal bool TrimFields { get; init; }
    internal bool TrimHeaders { get; init; }
    internal int BufferSize { get; init; } = 4096;
}
