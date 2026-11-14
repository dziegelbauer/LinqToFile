using System.Collections;
using System.Globalization;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Queries;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, deferred XLSX source with synchronous and asynchronous enumeration.</summary>
/// <remarks>Paths and factories open an independent, library-owned stream per enumeration. A supplied stream permits one enumeration across all derived configurations. Options do not read data.</remarks>
/// <typeparam name="T">The mapped record type.</typeparam>
public class XlsxSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly StreamSource source;
    private readonly XlsxOptions options;
    internal XlsxOptions Options => options;
    internal XlsxSource(StreamSource source, XlsxOptions options) { this.source = source; this.options = options; }
    internal virtual XlsxSource<T> Configure(XlsxOptions value) => new(source, value);

    /// <summary>Selects a worksheet by name. Sources default to the first worksheet in workbook order.</summary>
    /// <param name="name">The worksheet name. Source matching ignores case.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public virtual XlsxSource<T> WithWorksheet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Configure(options with { Worksheet = name });
    }

    /// <summary>Selects the physical worksheet row that contains column headers.</summary>
    /// <param name="oneBasedRowNumber">The one-based row number, from 1 through 1,048,576.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithHeaderRow(int oneBasedRowNumber)
    {
        if (oneBasedRowNumber is < 1 or > 1_048_576) throw new ArgumentOutOfRangeException(nameof(oneBasedRowNumber));
        return Configure(options with { HeaderRow = oneBasedRowNumber });
    }

    /// <summary>Uses a read-only culture snapshot for text conversion. Native XLSX numbers use invariant culture.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual XlsxSource<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }

    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public virtual XlsxSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return Configure(options with { HeaderComparison = value });
    }

    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual XlsxSource<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    /// <summary>Sets the directory for auxiliary workbook storage. Publication staging stays beside the destination.</summary>
    /// <param name="path">The directory path. The default is the system temporary directory.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual XlsxSource<T> WithTemporaryDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Configure(options with { TemporaryDirectory = Path.GetFullPath(path) });
    }
    /// <summary>Controls auxiliary temporary files. Enabled by default; XLSX mutation requires temporary files.</summary>
    /// <param name="enabled">Whether temporary files are permitted.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual XlsxSource<T> WithTemporaryFiles(bool enabled = true) => Configure(options with { TemporaryFiles = enabled });
    /// <summary>Sets the memory budget for shared strings before temporary storage is required.</summary>
    /// <param name="bytes">A nonnegative byte count. The default is 8 MiB.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithSharedStringMemoryBudget(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        return Configure(options with { SharedStringMemoryBudget = bytes });
    }
    /// <summary>Limits compressed workbook size, including staged mutation output.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithMaxWorkbookBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return Configure(options with { MaxWorkbookBytes = bytes });
    }
    /// <summary>Limits cumulative bytes decompressed from the source workbook across all passes.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithMaxDecompressedBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return Configure(options with { MaxDecompressedBytes = bytes });
    }
    /// <summary>Limits the aggregate live logical size of operation-owned temporary files.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithMaxTemporaryBytes(long? bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return Configure(options with { MaxTemporaryBytes = bytes });
    }
    /// <summary>Limits worksheet cell text length.</summary>
    /// <param name="length">A nonnegative number of UTF-16 code units, or null for no configured limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public virtual XlsxSource<T> WithMaxCellTextLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxCellTextLength = length });
    }
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public virtual XlsxSource<T> WithHeaderTrimming(bool enabled = true) => Configure(options with { TrimHeaders = enabled });
    /// <summary>Returns a deferred synchronous record sequence.</summary>
    /// <returns>A deferred sequence. Reading starts when enumeration advances.</returns>
    public IEnumerable<T> AsEnumerable() => this;
    /// <summary>Returns a deferred asynchronous record sequence.</summary>
    /// <returns>A deferred sequence. Reading starts when enumeration advances.</returns>
    public IAsyncEnumerable<T> AsAsyncEnumerable() => TabularQuery<T, XlsxOptions>.ReadAsync(source, options, XlsxRecordReaderFactory.Instance);
    /// <summary>Returns a deferred synchronous record enumerator.</summary>
    /// <returns>An enumerator that releases owned resources when disposed.</returns>
    public IEnumerator<T> GetEnumerator() => TabularQuery<T, XlsxOptions>.Read(source, options, XlsxRecordReaderFactory.Instance).GetEnumerator();
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
