using System.Globalization;

namespace LinqToFile.Reading;

internal enum ColumnBindingMode { Headers, SuppliedNames, Indexes }

internal abstract record TabularOptions
{
    internal ColumnBindingMode BindingMode { get; init; }
    internal string[] ColumnNames { get; init; } = [];
    internal StringComparer HeaderComparison { get; init; } = StringComparer.Ordinal;
    internal CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    internal bool TrimFields { get; init; }
    internal bool TrimHeaders { get; init; }
}
