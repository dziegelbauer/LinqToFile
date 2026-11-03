using LinqToFile.Infrastructure;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

internal sealed class CsvRecordReader(TextReader reader, CsvOptions options, System.Text.Encoding? encoding = null) : IRecordReader
{
    private readonly CsvParser parser = new(reader, options);
    public TabularRow Row => parser.Record;
    internal System.Text.Encoding Encoding => reader is StreamReader streamReader ? streamReader.CurrentEncoding : encoding!;
    internal long RecordByteOffset => parser.RecordByteOffset;
    internal bool EndsWithTerminator => parser.EndsWithTerminator;
    internal LineEnding? ReadRecordEnding() => parser.ReadRecordEnding();
    internal ValueTask<LineEnding?> ReadRecordEndingAsync(CancellationToken token) => parser.ReadRecordEndingAsync(token);
    public bool Read() => parser.Read();
    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken) => parser.ReadAsync(cancellationToken);

    public TabularFileException CreateFieldCountException(int expected)
    {
        int ordinal = Math.Min(Row.Count, expected);
        string? header = ordinal < Row.Headers.Length ? Row.Headers[ordinal] : null;
        string? member = ordinal < Row.TargetMembers.Length ? Row.TargetMembers[ordinal] : null;
        return new CsvFormatException(options.BindingMode == ColumnBindingMode.Headers
                ? "The record field count differs from the header."
                : "The record field count differs from the expected field count.",
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
