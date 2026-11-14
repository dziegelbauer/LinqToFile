using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, write-only CSV export destination. Each operation writes a complete document.</summary>
/// <remarks>A supplied stream permits one export across all derived configurations. Factory streams are disposed after each export. Stream exports can leave partial output on failure.</remarks>
/// <typeparam name="T">The mapped record type.</typeparam>
public sealed class CsvWriter<T> where T : class, ITabularRecord<T>
{
    private readonly StreamDestination destination;
    private readonly CsvOptions options;
    internal CsvWriter(StreamDestination destination, CsvOptions options)
    { this.destination = destination; this.options = options; }

    private CsvWriter<T> Configure(CsvOptions value) => new(destination, value);

    /// <summary>Uses a snapshot of the text encoding. The default is strict UTF-8 without an output BOM.</summary>
    /// <param name="value">The text encoding.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvWriter<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Configure(options with { Encoding = (Encoding)value.Clone() });
    }
    /// <summary>Uses a read-only culture snapshot for text conversion.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvWriter<T> WithCulture(CultureInfo value)
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
    public CsvWriter<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return Configure(options with { Delimiter = value });
    }
    /// <summary>Sets the CSV quote character. The default is a double quote.</summary>
    /// <param name="value">A character other than the delimiter, CR, LF, NUL, or a surrogate.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public CsvWriter<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return Configure(options with { Quote = value });
    }
    /// <summary>Sets the CSV record terminator. The default is LF.</summary>
    /// <param name="value">The record terminator.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public CsvWriter<T> WithNewLine(LineEnding value)
    {
        if (value is not (LineEnding.CrLf or LineEnding.Lf or LineEnding.Cr)) throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { NewLine = value });
    }
    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public CsvWriter<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return Configure(options with { HeaderComparison = value });
    }
    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvWriter<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvWriter<T> WithHeaderTrimming(bool enabled = true) => Configure(options with { TrimHeaders = enabled });
    /// <summary>Limits unescaped CSV field length before trimming, including headers.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero permits empty fields only.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public CsvWriter<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxFieldLength = length });
    }
    /// <summary>Limits CSV record length, including quotes and delimiters but excluding its terminator.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero is permitted.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public CsvWriter<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxRecordLength = length });
    }

    /// <summary>Writes a complete CSV document with headers only.</summary>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write() => CsvStreamExport<T>.Write(destination, options, []);
    /// <summary>Writes a complete CSV document with headers and one data record.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvStreamExport<T>.Write(destination, options, [record]);
    }
    /// <summary>Writes a complete CSV document with headers and the supplied data records.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.Write(destination, options, records);
    }
    /// <summary>Writes a complete CSV document with headers only asynchronously.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(CancellationToken cancellationToken = default) =>
        CsvStreamExport<T>.WriteAsync(destination, options, [], null, cancellationToken);
    /// <summary>Writes a complete CSV document with headers and one data record asynchronously.</summary>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvStreamExport<T>.WriteAsync(destination, options, [record], null, cancellationToken);
    }
    /// <summary>Writes a complete CSV document with headers and the supplied data records asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.WriteAsync(destination, options, records, null, cancellationToken);
    }
    /// <summary>Writes a complete CSV document with headers and the supplied data records asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.WriteAsync(destination, options, null, records, cancellationToken);
    }
}
