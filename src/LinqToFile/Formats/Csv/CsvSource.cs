using System.Collections;
using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Queries;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, deferred CSV source. Supplied streams share a single enumeration allowance.</summary>
/// <remarks>Paths and factories open an independent, library-owned stream per enumeration. A supplied stream permits one enumeration across all derived configurations. Options do not read data.</remarks>
/// <typeparam name="T">The mapped record type.</typeparam>
public class CsvSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly StreamSource source;
    private readonly CsvOptions options;
    internal CsvOptions Options => options;
    internal virtual CsvSource<T> Configure(CsvOptions value) => new(source, value);

    internal CsvSource(StreamSource source, CsvOptions options) { this.source = source; this.options = options; }

    /// <summary>Uses a snapshot of the text encoding. The default is strict UTF-8 without an output BOM.</summary>
    /// <param name="value">The text encoding.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Configure(options with { Encoding = (Encoding)value.Clone() });
    }

    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
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
    /// <param name="names">The column names in field order.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithColumnNames(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Length == 0) throw new ArgumentException("Supply at least one column name.", nameof(names));
        var configured = options with { BindingMode = ColumnBindingMode.SuppliedNames, ColumnNames = (string[])names.Clone() };
        HeaderlessSchema<T>.ValidateNames(configured);
        return Configure(configured);
    }

    /// <summary>Reads headerless CSV using ColumnIndex annotations on all mapped properties.</summary>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithColumnIndexes()
    {
        HeaderlessSchema<T>.ValidateIndexes();
        return Configure(options with { BindingMode = ColumnBindingMode.Indexes, ColumnNames = [] });
    }

    /// <summary>Reads column names from the first CSV record.</summary>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithHeaders() =>
        Configure(options with { BindingMode = ColumnBindingMode.Headers, ColumnNames = [] });

    /// <summary>Uses a read-only culture snapshot for text conversion.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }

    /// <summary>Sets the CSV field separator. The default is a comma.</summary>
    /// <param name="value">A character other than the quote, CR, LF, NUL, or a surrogate.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public virtual CsvSource<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return Configure(options with { Delimiter = value });
    }

    /// <summary>Sets the CSV quote character. The default is a double quote.</summary>
    /// <param name="value">A character other than the delimiter, CR, LF, NUL, or a surrogate.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public virtual CsvSource<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return Configure(options with { Quote = value });
    }

    /// <summary>Controls input encoding detection from a byte order mark. Enabled by default.</summary>
    /// <param name="enabled">Whether to detect the input encoding.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithBomDetection(bool enabled = true) => Configure(options with { DetectBom = enabled });
    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithHeaderTrimming(bool enabled = true)
    {
        var configured = options with { TrimHeaders = enabled };
        HeaderlessSchema<T>.ValidateNames(configured);
        return Configure(configured);
    }
    /// <summary>Controls skipping of blank physical CSV lines outside quoted fields. Enabled by default.</summary>
    /// <param name="enabled">Whether to skip blank lines.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual CsvSource<T> WithBlankLineSkipping(bool enabled = true) => Configure(options with { SkipBlankLines = enabled });

    /// <summary>Sets the decoded-character input buffer size. The default is 4096.</summary>
    /// <param name="size">A positive number of UTF-16 code units.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual CsvSource<T> WithBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return Configure(options with { BufferSize = size });
    }

    /// <summary>Limits unescaped CSV field length before trimming, including headers.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero permits empty fields only.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual CsvSource<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxFieldLength = length });
    }

    /// <summary>Limits CSV record length, including quotes and delimiters but excluding its terminator.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero is permitted.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual CsvSource<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxRecordLength = length });
    }

    /// <summary>Returns a deferred synchronous record sequence.</summary>
    /// <returns>A deferred sequence. Reading starts when enumeration advances.</returns>
    public IEnumerable<T> AsEnumerable() => this;
    /// <summary>Returns a deferred asynchronous record sequence.</summary>
    /// <returns>A deferred sequence. Reading starts when enumeration advances.</returns>
    public IAsyncEnumerable<T> AsAsyncEnumerable() => TabularQuery<T, CsvOptions>.ReadAsync(source, options, CsvRecordReaderFactory.Instance);
    /// <summary>Returns a deferred synchronous record enumerator.</summary>
    /// <returns>An enumerator that releases owned resources when disposed.</returns>
    public IEnumerator<T> GetEnumerator() => TabularQuery<T, CsvOptions>.Read(source, options, CsvRecordReaderFactory.Instance).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The async foreach pattern avoids exposing two competing sets of LINQ extension methods.
    /// <summary>Returns a deferred asynchronous record enumerator.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>An asynchronous enumerator that releases owned resources when disposed.</returns>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);

    /// <summary>Reads all records asynchronously into a list.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The collection of converted records.</returns>
    public ValueTask<List<T>> ToListAsync(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().ToListAsync(cancellationToken);

    /// <summary>Reads all records asynchronously into an array.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The collection of converted records.</returns>
    public ValueTask<T[]> ToArrayAsync(CancellationToken cancellationToken = default) =>
        AsAsyncEnumerable().ToArrayAsync(cancellationToken);

    /// <summary>Reads all records asynchronously into a dictionary.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <param name="keySelector">The function that selects each key.</param>
    /// <param name="comparer">The key equality comparer, or null for the default comparer.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The collection of converted records.</returns>
    /// <exception cref="ArgumentException">The input produces duplicate keys.</exception>
    public ValueTask<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null, CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return AsyncMaterialization.ToDictionaryAsync(AsAsyncEnumerable(), keySelector, static row => row,
            comparer, cancellationToken);
    }

    /// <summary>Reads all records asynchronously into a dictionary.</summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="keySelector">The function that selects each key.</param>
    /// <param name="valueSelector">The function that selects each dictionary value.</param>
    /// <param name="comparer">The key equality comparer, or null for the default comparer.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The collection of converted records.</returns>
    /// <exception cref="ArgumentException">The input produces duplicate keys.</exception>
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
