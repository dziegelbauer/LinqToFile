using System.Globalization;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable XLSX path source with reading, append, and insertion operations.</summary>
public sealed class XlsxFile<T> : XlsxSource<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    internal XlsxFile(string path, XlsxOptions options) : base(new StreamSource(path), options) => this.path = path;
    internal override XlsxSource<T> Configure(XlsxOptions value) => new XlsxFile<T>(path, value);
    public override XlsxFile<T> WithWorksheet(string name) => (XlsxFile<T>)base.WithWorksheet(name);
    public override XlsxFile<T> WithHeaderRow(int oneBasedRowNumber) => (XlsxFile<T>)base.WithHeaderRow(oneBasedRowNumber);
    public override XlsxFile<T> WithCulture(CultureInfo value) => (XlsxFile<T>)base.WithCulture(value);
    public override XlsxFile<T> WithHeaderComparison(StringComparer value) => (XlsxFile<T>)base.WithHeaderComparison(value);
    public override XlsxFile<T> WithFieldTrimming(bool enabled = true) => (XlsxFile<T>)base.WithFieldTrimming(enabled);
    public override XlsxFile<T> WithHeaderTrimming(bool enabled = true) => (XlsxFile<T>)base.WithHeaderTrimming(enabled);
    public override XlsxFile<T> WithTemporaryDirectory(string directory) => (XlsxFile<T>)base.WithTemporaryDirectory(directory);
    public override XlsxFile<T> WithTemporaryFiles(bool enabled = true) => (XlsxFile<T>)base.WithTemporaryFiles(enabled);
    public override XlsxFile<T> WithSharedStringMemoryBudget(long bytes) => (XlsxFile<T>)base.WithSharedStringMemoryBudget(bytes);
    public override XlsxFile<T> WithMaxWorkbookBytes(long? bytes) => (XlsxFile<T>)base.WithMaxWorkbookBytes(bytes);
    public override XlsxFile<T> WithMaxDecompressedBytes(long? bytes) => (XlsxFile<T>)base.WithMaxDecompressedBytes(bytes);
    public override XlsxFile<T> WithMaxTemporaryBytes(long? bytes) => (XlsxFile<T>)base.WithMaxTemporaryBytes(bytes);
    public override XlsxFile<T> WithMaxCellTextLength(int? length) => (XlsxFile<T>)base.WithMaxCellTextLength(length);

    public long Append(T record)
    { ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.Write(path, Options, null, [record]); }
    public long AppendRange(IEnumerable<T> records)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.Write(path, Options, null, records); }
    public ValueTask<long> AppendAsync(T record, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.WriteAsync(path, Options, null, [record], null, cancellationToken); }
    public ValueTask<long> AppendRangeAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, null, records, null, cancellationToken); }
    public ValueTask<long> AppendRangeAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, null, null, records, cancellationToken); }

    public long Insert(long position, T record)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.Write(path, Options, position, [record]); }
    public long InsertRange(long position, IEnumerable<T> records)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.Write(path, Options, position, records); }
    public ValueTask<long> InsertAsync(long position, T record, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(record); return XlsxMutation<T>.WriteAsync(path, Options, position, [record], null, cancellationToken); }
    public ValueTask<long> InsertRangeAsync(long position, IEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, position, records, null, cancellationToken); }
    public ValueTask<long> InsertRangeAsync(long position, IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    { ArgumentOutOfRangeException.ThrowIfNegative(position); ArgumentNullException.ThrowIfNull(records); return XlsxMutation<T>.WriteAsync(path, Options, position, null, records, cancellationToken); }
}
