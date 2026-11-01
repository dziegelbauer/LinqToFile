using LinqToFile.Infrastructure;
using LinqToFile.Queries;
using LinqToFile.Sources;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class FormatIndependenceTests
{
    [Fact]
    public async Task NonCsvReaderReusesSourcesBindingMappingAndBothQueryModes()
    {
        // This synthetic format stores one numeric record per byte, with an implicit header.
        var syncStream = new ProbeStream([1, 2, 3]);
        var sync = TabularQuery<NumberRow, ByteOptions>.Read(new StreamSource(syncStream, false),
            new ByteOptions(), ByteReaderFactory.Instance).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, sync.Select(r => r.Value));
        Assert.Equal(1, syncStream.SyncDisposals);

        var asyncStream = new ProbeStream([1, 2, 3], 1, asyncOnly: true, yieldReads: true);
        var rows = await TabularQuery<NumberRow, ByteOptions>.ReadAsync(new StreamSource(asyncStream, false),
            new ByteOptions(), ByteReaderFactory.Instance).ToArrayAsync();
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Value));
        Assert.Equal(0, asyncStream.SyncReads);
        Assert.Equal(1, asyncStream.AsyncDisposals);
    }

    private sealed record ByteOptions : TabularOptions;
    [Fact]
    public void NativeNumbersFromAnotherFormatDoNotImplicitlyUseExcelDateRules()
    {
        var stream = new ProbeStream([1]);
        Assert.Throws<TabularConversionException>(() => TabularQuery<SheetDateTime, ByteOptions>
            .Read(new StreamSource(stream, false), new ByteOptions(), ByteReaderFactory.Instance).Single());
        Assert.Equal(1, stream.SyncDisposals);
    }

    private sealed class ByteReaderFactory : IRecordReaderFactory<ByteOptions>
    {
        internal static readonly ByteReaderFactory Instance = new();
        public IRecordReader Open(Stream stream, ByteOptions options) => new ByteReader(stream);
        public ValueTask<IRecordReader> OpenAsync(Stream stream, ByteOptions options, CancellationToken cancellationToken) =>
            new((IRecordReader)new ByteReader(stream));
    }

    private sealed class ByteReader(Stream stream) : IRecordReader
    {
        public TabularRow Row { get; } = new(16, null);
        private readonly byte[] value = new byte[1];
        private bool header = true;
        private long number;

        public bool Read()
        {
            if (header) return Header();
            return stream.Read(value, 0, 1) == 1 && Data();
        }

        public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (header) return Header();
            return await stream.ReadAsync(value.AsMemory(), cancellationToken) == 1 && Data();
        }

        private bool Header()
        {
            header = false;
            Row.Reset();
            foreach (char c in "Value") Row.Append(c);
            Row.EndField();
            Row.RecordNumber = ++number;
            Row.LineNumber = Row.FailureLineNumber = number;
            return true;
        }

        private bool Data()
        {
            Row.Reset();
            Row.Append((char)('0' + value[0]));
            Row.EndField(CellKind.Number);
            Row.RecordNumber = ++number;
            Row.LineNumber = Row.FailureLineNumber = number;
            return true;
        }

        public TabularFileException CreateFieldCountException(int expected) =>
            new TabularSchemaException("Invalid synthetic record width.", number, number, number);
        public void Dispose() { }
        public ValueTask DisposeAsync() => default;
    }
}
