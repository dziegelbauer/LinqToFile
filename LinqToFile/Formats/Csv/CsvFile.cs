using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable CSV path configuration with read, creation, append, and insertion operations.</summary>
public sealed class CsvFile<T> : CsvSource<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    internal CsvFile(string path, CsvOptions options) : base(new StreamSource(path), options) => this.path = path;
    internal override CsvSource<T> Configure(CsvOptions value) => new CsvFile<T>(path, value);
    public override CsvFile<T> WithEncoding(Encoding value) => (CsvFile<T>)base.WithEncoding(value);
    public override CsvFile<T> WithHeaderComparison(StringComparer value) => (CsvFile<T>)base.WithHeaderComparison(value);
    public override CsvFile<T> WithCulture(CultureInfo value) => (CsvFile<T>)base.WithCulture(value);
    public override CsvFile<T> WithDelimiter(char value) => (CsvFile<T>)base.WithDelimiter(value);
    public override CsvFile<T> WithQuote(char value) => (CsvFile<T>)base.WithQuote(value);
    public override CsvFile<T> WithBomDetection(bool enabled = true) => (CsvFile<T>)base.WithBomDetection(enabled);
    public override CsvFile<T> WithFieldTrimming(bool enabled = true) => (CsvFile<T>)base.WithFieldTrimming(enabled);
    public override CsvFile<T> WithHeaderTrimming(bool enabled = true) => (CsvFile<T>)base.WithHeaderTrimming(enabled);
    public override CsvFile<T> WithBlankLineSkipping(bool enabled = true) => (CsvFile<T>)base.WithBlankLineSkipping(enabled);
    public override CsvFile<T> WithBufferSize(int size) => (CsvFile<T>)base.WithBufferSize(size);
    public override CsvFile<T> WithMaxFieldLength(int? length) => (CsvFile<T>)base.WithMaxFieldLength(length);
    public override CsvFile<T> WithMaxRecordLength(int? length) => (CsvFile<T>)base.WithMaxRecordLength(length);

    public CsvFile<T> WithInPlaceWrites(bool enabled = true) =>
        (CsvFile<T>)Configure(Options with { InPlaceWrites = enabled });
    public CsvFile<T> WithAppendValidation(bool enabled = true) =>
        (CsvFile<T>)Configure(Options with { ValidateAppend = enabled });

    public long Insert(long position, T record)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(record);
        return CsvInsertion<T>.Insert(path, Options, position, [record]);
    }
    public long InsertRange(long position, IEnumerable<T> records)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.Insert(path, Options, position, records);
    }
    public ValueTask<long> InsertAsync(long position, T record, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(record);
        return CsvInsertion<T>.InsertAsync(path, Options, position, [record], null, cancellationToken);
    }
    public ValueTask<long> InsertRangeAsync(long position, IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.InsertAsync(path, Options, position, records, null, cancellationToken);
    }
    public ValueTask<long> InsertRangeAsync(long position, IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.InsertAsync(path, Options, position, null, records, cancellationToken);
    }

    public long Append(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvAppend<T>.Append(path, Options, [record]);
    }
    public long AppendRange(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.Append(path, Options, records);
    }
    public ValueTask<long> AppendAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvAppend<T>.AppendAsync(path, Options, [record], null, cancellationToken);
    }
    public ValueTask<long> AppendRangeAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.AppendAsync(path, Options, records, null, cancellationToken);
    }
    public ValueTask<long> AppendRangeAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.AppendAsync(path, Options, null, records, cancellationToken);
    }

    public CsvFile<T> WithNewLine(LineEnding value)
    {
        if (value is not (LineEnding.CrLf or LineEnding.Lf or LineEnding.Cr))
            throw new ArgumentOutOfRangeException(nameof(value));
        return (CsvFile<T>)Configure(Options with { NewLine = value });
    }

    public long Create() => CsvCreation<T>.Create(path, Options, []);
    public long Create(IEnumerable<T> initialRecords)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.Create(path, Options, initialRecords);
    }
    public ValueTask<long> CreateAsync(CancellationToken cancellationToken = default) =>
        CsvCreation<T>.CreateAsync(path, Options, [], null, cancellationToken);
    public ValueTask<long> CreateAsync(IEnumerable<T> initialRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.CreateAsync(path, Options, initialRecords, null, cancellationToken);
    }
    public ValueTask<long> CreateAsync(IAsyncEnumerable<T> initialRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.CreateAsync(path, Options, null, initialRecords, cancellationToken);
    }
}
