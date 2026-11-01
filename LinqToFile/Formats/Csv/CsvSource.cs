using System.Collections;
using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Queries;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, deferred CSV source. Supplied streams share a single enumeration allowance.</summary>
public sealed class CsvSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly StreamSource source;
    private readonly CsvOptions options;

    internal CsvSource(StreamSource source, CsvOptions options) { this.source = source; this.options = options; }

    public CsvSource<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(source, options with { Encoding = (Encoding)value.Clone() });
    }

    public CsvSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return new(source, options with { HeaderComparison = value });
    }

    public CsvSource<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(source, options with { Culture = CultureInfo.ReadOnly((CultureInfo)value.Clone()) });
    }

    public CsvSource<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return new(source, options with { Delimiter = value });
    }

    public CsvSource<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return new(source, options with { Quote = value });
    }

    public CsvSource<T> WithBomDetection(bool enabled = true) => new(source, options with { DetectBom = enabled });
    public CsvSource<T> WithFieldTrimming(bool enabled = true) => new(source, options with { TrimFields = enabled });
    public CsvSource<T> WithHeaderTrimming(bool enabled = true) => new(source, options with { TrimHeaders = enabled });
    public CsvSource<T> WithBlankLineSkipping(bool enabled = true) => new(source, options with { SkipBlankLines = enabled });

    public CsvSource<T> WithBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return new(source, options with { BufferSize = size });
    }

    public CsvSource<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return new(source, options with { MaxFieldLength = length });
    }

    public CsvSource<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return new(source, options with { MaxRecordLength = length });
    }

    public IEnumerable<T> AsEnumerable() => this;
    public IAsyncEnumerable<T> AsAsyncEnumerable() => TabularQuery<T, CsvOptions>.ReadAsync(source, options, CsvRecordReaderFactory.Instance);
    public IEnumerator<T> GetEnumerator() => TabularQuery<T, CsvOptions>.Read(source, options, CsvRecordReaderFactory.Instance).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The async foreach pattern avoids exposing two competing sets of LINQ extension methods.
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);

    public ValueTask<List<T>> ToListAsync(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().ToListAsync(cancellationToken);

    public ValueTask<T[]> ToArrayAsync(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().ToArrayAsync(cancellationToken);

    public ValueTask<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null, CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return AsyncMaterialization.ToDictionaryAsync(AsAsyncEnumerable(), keySelector, static row => row,
            comparer, cancellationToken);
    }

    public ValueTask<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(Func<T, TKey> keySelector,
        Func<T, TValue> valueSelector, IEqualityComparer<TKey>? comparer = null, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);
        return AsyncMaterialization.ToDictionaryAsync(AsAsyncEnumerable(), keySelector, valueSelector,
            comparer, cancellationToken);
    }
}
