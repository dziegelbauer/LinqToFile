using LinqToFile.Infrastructure;

namespace LinqToFile.Reading;

// A format supplies records and format errors; shared query code owns sources, binding, and mapping.
internal interface IRecordReader : IDisposable, IAsyncDisposable
{
    TabularRow Row { get; }
    bool Read();
    ValueTask<bool> ReadAsync(CancellationToken cancellationToken);
    TabularFileException CreateFieldCountException(int expected);
}

internal interface IRecordReaderFactory<in TOptions> where TOptions : TabularOptions
{
    IRecordReader Open(Stream stream, TOptions options);
    ValueTask<IRecordReader> OpenAsync(Stream stream, TOptions options, CancellationToken cancellationToken);
}
