using LinqToFile.Infrastructure;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

internal sealed class CsvRecordReader(StreamReader reader, CsvOptions options) : IRecordReader
{
    private readonly CsvParser parser = new(reader, options);
    public TabularRow Row => parser.Record;
    public bool Read() => parser.Read();
    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken) => parser.ReadAsync(cancellationToken);

    public TabularFileException CreateFieldCountException(int expected)
    {
        int ordinal = Math.Min(Row.Count, expected);
        string? header = ordinal < Row.Headers.Length ? Row.Headers[ordinal] : null;
        string? member = ordinal < Row.TargetMembers.Length ? Row.TargetMembers[ordinal] : null;
        return new CsvFormatException("The record field count differs from the header.",
            Row.RecordNumber, Row.LineNumber, Row.FailureLineNumber, ordinal + 1, header, member);
    }

    // StreamReader only owns managed decoder/prefix state here. The source lease owns actual I/O disposal.
    public void Dispose() => reader.Dispose();
    public ValueTask DisposeAsync() { Dispose(); return default; }
}

internal sealed class CsvRecordReaderFactory : IRecordReaderFactory<CsvOptions>
{
    internal static readonly CsvRecordReaderFactory Instance = new();
    private CsvRecordReaderFactory() { }
    public IRecordReader Open(Stream stream, CsvOptions options) =>
        new CsvRecordReader(EncodedTextReader.Open(stream, options, leaveOpen: true), options);

    public async ValueTask<IRecordReader> OpenAsync(Stream stream, CsvOptions options, CancellationToken cancellationToken) =>
        new CsvRecordReader(await EncodedTextReader.OpenAsync(stream, options, cancellationToken, leaveOpen: true)
            .ConfigureAwait(false), options);
}
