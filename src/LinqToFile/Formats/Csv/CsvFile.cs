using System.Globalization;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable CSV path configuration with read, creation, append, and insertion operations.</summary>
/// <typeparam name="T">The mapped record type.</typeparam>
public sealed class CsvFile<T> : CsvSource<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    internal CsvFile(string path, CsvOptions options) : base(new StreamSource(path), options) => this.path = path;
    internal override CsvSource<T> Configure(CsvOptions value) => new CsvFile<T>(path, value);
    /// <summary>Reads headerless CSV with the supplied column names. The first record contains data.</summary>
    /// <param name="names">The column names in field order.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithColumnNames(params string[] names) => (CsvFile<T>)base.WithColumnNames(names);
    /// <summary>Reads headerless CSV with zero-based ColumnIndex annotations on all mapped properties.</summary>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithColumnIndexes() => (CsvFile<T>)base.WithColumnIndexes();
    /// <summary>Reads column names from the first CSV record. This is the default binding mode.</summary>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithHeaders() => (CsvFile<T>)base.WithHeaders();
    /// <summary>Uses a snapshot of the text encoding. The default is strict UTF-8 without an output BOM.</summary>
    /// <param name="value">The text encoding.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithEncoding(Encoding value) => (CsvFile<T>)base.WithEncoding(value);
    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithHeaderComparison(StringComparer value) => (CsvFile<T>)base.WithHeaderComparison(value);
    /// <summary>Uses a read-only culture snapshot for text conversion.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithCulture(CultureInfo value) => (CsvFile<T>)base.WithCulture(value);
    /// <summary>Sets the CSV field separator. The default is a comma.</summary>
    /// <param name="value">A character other than the quote, CR, LF, NUL, or a surrogate.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithDelimiter(char value) => (CsvFile<T>)base.WithDelimiter(value);
    /// <summary>Sets the CSV quote character. The default is a double quote.</summary>
    /// <param name="value">A character other than the delimiter, CR, LF, NUL, or a surrogate.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithQuote(char value) => (CsvFile<T>)base.WithQuote(value);
    /// <summary>Controls input encoding detection from a byte order mark. Enabled by default.</summary>
    /// <param name="enabled">Whether to detect the input encoding.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithBomDetection(bool enabled = true) => (CsvFile<T>)base.WithBomDetection(enabled);
    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithFieldTrimming(bool enabled = true) => (CsvFile<T>)base.WithFieldTrimming(enabled);
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithHeaderTrimming(bool enabled = true) => (CsvFile<T>)base.WithHeaderTrimming(enabled);
    /// <summary>Controls skipping of blank physical CSV lines outside quoted fields. Enabled by default.</summary>
    /// <param name="enabled">Whether to skip blank lines.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithBlankLineSkipping(bool enabled = true) => (CsvFile<T>)base.WithBlankLineSkipping(enabled);
    /// <summary>Sets the decoded-character input buffer size. The default is 4096.</summary>
    /// <param name="size">A positive number of UTF-16 code units.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithBufferSize(int size) => (CsvFile<T>)base.WithBufferSize(size);
    /// <summary>Limits unescaped CSV field length before trimming, including headers.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero permits empty fields only.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithMaxFieldLength(int? length) => (CsvFile<T>)base.WithMaxFieldLength(length);
    /// <summary>Limits CSV record length, including quotes and delimiters but excluding its terminator.</summary>
    /// <param name="length">The maximum UTF-16 code units; null removes the limit. Zero is permitted.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override CsvFile<T> WithMaxRecordLength(int? length) => (CsvFile<T>)base.WithMaxRecordLength(length);

    /// <summary>Controls direct CSV mutation. Disabled by default; enabled writes can leave partial changes on failure.</summary>
    /// <param name="enabled">Whether to write directly without staged replacement.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvFile<T> WithInPlaceWrites(bool enabled = true) =>
        (CsvFile<T>)Configure(Options with { InPlaceWrites = enabled });
    /// <summary>Controls full validation of existing CSV data during append. Enabled by default; insertion always validates.</summary>
    /// <param name="enabled">Whether to validate existing data during append.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvFile<T> WithAppendValidation(bool enabled = true) =>
        (CsvFile<T>)Configure(Options with { ValidateAppend = enabled });

    /// <summary>Sets whether complete-document creation can replace an existing path. The default is Throw.</summary>
    /// <param name="value">The publication behavior. This option does not change append or insertion.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public CsvFile<T> WithExistingFileBehavior(ExistingFileBehavior value)
    {
        if (value is not (ExistingFileBehavior.Throw or ExistingFileBehavior.Overwrite))
            throw new ArgumentOutOfRangeException(nameof(value));
        return (CsvFile<T>)Configure(Options with { ExistingFileBehavior = value });
    }

    /// <summary>Inserts one record into the existing file at a data-record index.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Insert(long position, T record)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(record);
        return CsvInsertion<T>.Insert(path, Options, position, [record]);
    }
    /// <summary>Inserts records into the existing file at a data-record index.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long InsertRange(long position, IEnumerable<T> records)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.Insert(path, Options, position, records);
    }
    /// <summary>Inserts one record into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> InsertAsync(long position, T record, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(record);
        return CsvInsertion<T>.InsertAsync(path, Options, position, [record], null, cancellationToken);
    }
    /// <summary>Inserts records into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> InsertRangeAsync(long position, IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.InsertAsync(path, Options, position, records, null, cancellationToken);
    }
    /// <summary>Inserts records into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> InsertRangeAsync(long position, IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(records);
        return CsvInsertion<T>.InsertAsync(path, Options, position, null, records, cancellationToken);
    }

    /// <summary>Appends one record to the existing file.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Append(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvAppend<T>.Append(path, Options, [record]);
    }
    /// <summary>Appends records to the existing file.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long AppendRange(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.Append(path, Options, records);
    }
    /// <summary>Appends one record to the existing file. Executes asynchronously.</summary>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> AppendAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CsvAppend<T>.AppendAsync(path, Options, [record], null, cancellationToken);
    }
    /// <summary>Appends records to the existing file. Executes asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> AppendRangeAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.AppendAsync(path, Options, records, null, cancellationToken);
    }
    /// <summary>Appends records to the existing file. Executes asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> AppendRangeAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return CsvAppend<T>.AppendAsync(path, Options, null, records, cancellationToken);
    }

    /// <summary>Sets the terminator for new CSV records. New files use LF by default; mutations use the header terminator.</summary>
    /// <param name="value">The record terminator.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public CsvFile<T> WithNewLine(LineEnding value)
    {
        if (value is not (LineEnding.CrLf or LineEnding.Lf or LineEnding.Cr))
            throw new ArgumentOutOfRangeException(nameof(value));
        return (CsvFile<T>)Configure(Options with { NewLine = value });
    }

    /// <summary>Creates a complete CSV document with headers only.</summary>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>An existing destination is rejected unless overwrite is configured. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Create() => CsvCreation<T>.Create(path, Options, []);
    /// <summary>Creates a complete CSV document with headers and the supplied data records.</summary>
    /// <param name="initialRecords">The initial data records, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>An existing destination is rejected unless overwrite is configured. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Create(IEnumerable<T> initialRecords)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.Create(path, Options, initialRecords);
    }
    /// <summary>Creates a complete CSV document with headers only asynchronously.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>An existing destination is rejected unless overwrite is configured. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> CreateAsync(CancellationToken cancellationToken = default) =>
        CsvCreation<T>.CreateAsync(path, Options, [], null, cancellationToken);
    /// <summary>Creates a complete CSV document with headers and the supplied data records asynchronously.</summary>
    /// <param name="initialRecords">The initial data records, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>An existing destination is rejected unless overwrite is configured. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> CreateAsync(IEnumerable<T> initialRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.CreateAsync(path, Options, initialRecords, null, cancellationToken);
    }
    /// <summary>Creates a complete CSV document with headers and the supplied data records asynchronously.</summary>
    /// <param name="initialRecords">The initial data records, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>An existing destination is rejected unless overwrite is configured. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success. In-place writes stop observing cancellation before the first destination change; later failures can leave partial output.</remarks>
    public ValueTask<long> CreateAsync(IAsyncEnumerable<T> initialRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialRecords);
        return CsvCreation<T>.CreateAsync(path, Options, null, initialRecords, cancellationToken);
    }
}
