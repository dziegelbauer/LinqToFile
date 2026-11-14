using System.Globalization;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable XLSX path source with reading, append, and insertion operations.</summary>
/// <typeparam name="T">The mapped record type.</typeparam>
public sealed class XlsxFile<T> : XlsxSource<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    internal XlsxFile(string path, XlsxOptions options) : base(new StreamSource(path), options) => this.path = path;
    internal override XlsxSource<T> Configure(XlsxOptions value) => new XlsxFile<T>(path, value);
    /// <summary>Selects a worksheet by name. Sources default to the first worksheet in workbook order.</summary>
    /// <param name="name">The worksheet name. Source matching ignores case.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithWorksheet(string name) => (XlsxFile<T>)base.WithWorksheet(name);
    /// <summary>Selects the physical worksheet row that contains column headers.</summary>
    /// <param name="oneBasedRowNumber">The one-based row number, from 1 through 1,048,576.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithHeaderRow(int oneBasedRowNumber) => (XlsxFile<T>)base.WithHeaderRow(oneBasedRowNumber);
    /// <summary>Uses a read-only culture snapshot for text conversion. Native XLSX numbers use invariant culture.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithCulture(CultureInfo value) => (XlsxFile<T>)base.WithCulture(value);
    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithHeaderComparison(StringComparer value) => (XlsxFile<T>)base.WithHeaderComparison(value);
    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithFieldTrimming(bool enabled = true) => (XlsxFile<T>)base.WithFieldTrimming(enabled);
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithHeaderTrimming(bool enabled = true) => (XlsxFile<T>)base.WithHeaderTrimming(enabled);
    /// <summary>Sets the directory for auxiliary workbook storage. Publication staging stays beside the destination.</summary>
    /// <param name="directory">The directory path. The default is the system temporary directory.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithTemporaryDirectory(string directory) => (XlsxFile<T>)base.WithTemporaryDirectory(directory);
    /// <summary>Controls auxiliary temporary files. Enabled by default; XLSX mutation requires temporary files.</summary>
    /// <param name="enabled">Whether temporary files are permitted.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithTemporaryFiles(bool enabled = true) => (XlsxFile<T>)base.WithTemporaryFiles(enabled);
    /// <summary>Sets the memory budget for shared strings before temporary storage is required.</summary>
    /// <param name="bytes">A nonnegative byte count. The default is 8 MiB.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithSharedStringMemoryBudget(long bytes) => (XlsxFile<T>)base.WithSharedStringMemoryBudget(bytes);
    /// <summary>Limits compressed workbook size, including staged mutation output.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithMaxWorkbookBytes(long? bytes) => (XlsxFile<T>)base.WithMaxWorkbookBytes(bytes);
    /// <summary>Limits cumulative bytes decompressed from the source workbook across all passes.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithMaxDecompressedBytes(long? bytes) => (XlsxFile<T>)base.WithMaxDecompressedBytes(bytes);
    /// <summary>Limits the aggregate live logical size of operation-owned temporary files.</summary>
    /// <param name="bytes">A nonnegative byte count, or null for no limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithMaxTemporaryBytes(long? bytes) => (XlsxFile<T>)base.WithMaxTemporaryBytes(bytes);
    /// <summary>Limits worksheet cell text length.</summary>
    /// <param name="length">A nonnegative number of UTF-16 code units, or null for no configured limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public override XlsxFile<T> WithMaxCellTextLength(int? length) => (XlsxFile<T>)base.WithMaxCellTextLength(length);

    /// <summary>Appends one record to the existing file.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Append(T record)
    { ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.Write(path, Options, null, [record]); }
    /// <summary>Appends records to the existing file.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long AppendRange(IEnumerable<T> records)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.Write(path, Options, null, records); }
    /// <summary>Appends one record to the existing file. Executes asynchronously.</summary>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> AppendAsync(T record, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.WriteAsync(path, Options, null, [record], null, cancellationToken); }
    /// <summary>Appends records to the existing file. Executes asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> AppendRangeAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, null, records, null, cancellationToken); }
    /// <summary>Appends records to the existing file. Executes asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> AppendRangeAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, null, null, records, cancellationToken); }

    /// <summary>Inserts one record into the existing file at a data-record index.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long Insert(long position, T record)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.Write(path, Options, position, [record]); }
    /// <summary>Inserts records into the existing file at a data-record index.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds.</remarks>
    public long InsertRange(long position, IEnumerable<T> records)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.Write(path, Options, position, records); }
    /// <summary>Inserts one record into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> InsertAsync(long position, T record, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.WriteAsync(path, Options, position, [record], null, cancellationToken); }
    /// <summary>Inserts records into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> InsertRangeAsync(long position, IEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, position, records, null, cancellationToken); }
    /// <summary>Inserts records into the existing file at a data-record index. Executes asynchronously.</summary>
    /// <param name="position">The zero-based data-record index, excluding headers and skipped blank rows. The existing count appends.</param>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>The destination must exist. Empty ranges still validate the existing document. The default mode stages output beside the destination and publishes after preparation succeeds. Cancellation is observed until publication begins. Successful publication reports success.</remarks>
    public ValueTask<long> InsertRangeAsync(long position, IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, position, null, records, cancellationToken); }
}
