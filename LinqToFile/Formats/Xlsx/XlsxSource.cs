using System.Collections;
using System.Globalization;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Queries;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, deferred XLSX source for synchronous seekable input.</summary>
public sealed class XlsxSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly StreamSource source;
    private readonly XlsxOptions options;
    internal XlsxSource(StreamSource source, XlsxOptions options) { this.source = source; this.options = options; }

    public XlsxSource<T> WithWorksheet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(source, options with { Worksheet = name });
    }

    public XlsxSource<T> WithHeaderRow(int oneBasedRowNumber)
    {
        if (oneBasedRowNumber is < 1 or > 1_048_576) throw new ArgumentOutOfRangeException(nameof(oneBasedRowNumber));
        return new(source, options with { HeaderRow = oneBasedRowNumber });
    }

    public XlsxSource<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(source, options with { Culture = CultureInfo.ReadOnly((CultureInfo)value.Clone()) });
    }

    public XlsxSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return new(source, options with { HeaderComparison = value });
    }

    public XlsxSource<T> WithFieldTrimming(bool enabled = true) => new(source, options with { TrimFields = enabled });
    public XlsxSource<T> WithTemporaryDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(source, options with { TemporaryDirectory = Path.GetFullPath(path) });
    }
    public XlsxSource<T> WithTemporaryFiles(bool enabled = true) => new(source, options with { TemporaryFiles = enabled });
    public XlsxSource<T> WithSharedStringMemoryBudget(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        return new(source, options with { SharedStringMemoryBudget = bytes });
    }
    public XlsxSource<T> WithMaxWorkbookBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return new(source, options with { MaxWorkbookBytes = bytes });
    }
    public XlsxSource<T> WithMaxDecompressedBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return new(source, options with { MaxDecompressedBytes = bytes });
    }
    public XlsxSource<T> WithMaxTemporaryBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return new(source, options with { MaxTemporaryBytes = bytes });
    }
    public XlsxSource<T> WithMaxCellTextLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return new(source, options with { MaxCellTextLength = length });
    }
    public XlsxSource<T> WithHeaderTrimming(bool enabled = true) => new(source, options with { TrimHeaders = enabled });
    public IEnumerable<T> AsEnumerable() => this;
    public IEnumerator<T> GetEnumerator() => TabularQuery<T, XlsxOptions>.Read(source, options, XlsxRecordReaderFactory.Instance).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
