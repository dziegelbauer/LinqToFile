using System.Collections;
using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Queries;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, deferred CSV source. Supplied streams share a single enumeration allowance.</summary>
public class CsvSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly StreamSource source;
    private readonly CsvOptions options;
    internal CsvOptions Options => options;
    internal virtual CsvSource<T> Configure(CsvOptions value) => new(source, value);

    internal CsvSource(StreamSource source, CsvOptions options) { this.source = source; this.options = options; }

    public virtual CsvSource<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Configure(options with { Encoding = (Encoding)value.Clone() });
    }

    public virtual CsvSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        var configured = options with { HeaderComparison = value };
        HeaderlessSchema<T>.ValidateNames(configured);
        return Configure(configured);
    }

    /// <summary>Reads headerless CSV with a copied list of column names in file order.</summary>
    public virtual CsvSource<T> WithColumnNames(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Length == 0) throw new ArgumentException("Supply at least one column name.", nameof(names));
        var configured = options with { BindingMode = ColumnBindingMode.SuppliedNames, ColumnNames = (string[])names.Clone() };
        HeaderlessSchema<T>.ValidateNames(configured);
        return Configure(configured);
    }

    /// <summary>Reads headerless CSV using ColumnIndex annotations on all mapped properties.</summary>
    public virtual CsvSource<T> WithColumnIndexes()
    {
        HeaderlessSchema<T>.ValidateIndexes();
        return Configure(options with { BindingMode = ColumnBindingMode.Indexes, ColumnNames = [] });
    }

    /// <summary>Reads column names from the first CSV record.</summary>
    public virtual CsvSource<T> WithHeaders() =>
        Configure(options with { BindingMode = ColumnBindingMode.Headers, ColumnNames = [] });

    public virtual CsvSource<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }

    public virtual CsvSource<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return Configure(options with { Delimiter = value });
    }

    public virtual CsvSource<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return Configure(options with { Quote = value });
    }

    public virtual CsvSource<T> WithBomDetection(bool enabled = true) => Configure(options with { DetectBom = enabled });
    public virtual CsvSource<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    public virtual CsvSource<T> WithHeaderTrimming(bool enabled = true)
    {
        var configured = options with { TrimHeaders = enabled };
        HeaderlessSchema<T>.ValidateNames(configured);
        return Configure(configured);
    }
    public virtual CsvSource<T> WithBlankLineSkipping(bool enabled = true) => Configure(options with { SkipBlankLines = enabled });

    public virtual CsvSource<T> WithBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return Configure(options with { BufferSize = size });
    }

    public virtual CsvSource<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxFieldLength = length });
    }

    public virtual CsvSource<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxRecordLength = length });
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
