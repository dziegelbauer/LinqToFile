using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable, write-only CSV export destination. Each operation writes a complete document.</summary>
public sealed class CsvWriter<T> where T : class, ITabularRecord<T>
{
    private readonly StreamDestination destination;
    private readonly CsvOptions options;
    internal CsvWriter(StreamDestination destination, CsvOptions options)
    { this.destination = destination; this.options = options; }

    private CsvWriter<T> Configure(CsvOptions value) => new(destination, value);

    public CsvWriter<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Configure(options with { Encoding = (Encoding)value.Clone() });
    }
    public CsvWriter<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }
    public CsvWriter<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return Configure(options with { Delimiter = value });
    }
    public CsvWriter<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return Configure(options with { Quote = value });
    }
    public CsvWriter<T> WithNewLine(LineEnding value)
    {
        if (value is not (LineEnding.CrLf or LineEnding.Lf or LineEnding.Cr)) throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { NewLine = value });
    }
    public CsvWriter<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return Configure(options with { HeaderComparison = value });
    }
    public CsvWriter<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    public CsvWriter<T> WithHeaderTrimming(bool enabled = true) => Configure(options with { TrimHeaders = enabled });
    public CsvWriter<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxFieldLength = length });
    }
    public CsvWriter<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxRecordLength = length });
    }

    public long Write() => CsvStreamExport<T>.Write(destination, options, []);
    public long Write(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvStreamExport<T>.Write(destination, options, [record]);
    }
    public long Write(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.Write(destination, options, records);
    }
    public ValueTask<long> WriteAsync(CancellationToken cancellationToken = default) =>
        CsvStreamExport<T>.WriteAsync(destination, options, [], null, cancellationToken);
    public ValueTask<long> WriteAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvStreamExport<T>.WriteAsync(destination, options, [record], null, cancellationToken);
    }
    public ValueTask<long> WriteAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.WriteAsync(destination, options, records, null, cancellationToken);
    }
    public ValueTask<long> WriteAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvStreamExport<T>.WriteAsync(destination, options, null, records, cancellationToken);
    }
}
