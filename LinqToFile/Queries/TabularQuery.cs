using System.Runtime.CompilerServices;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile.Queries;

internal static class TabularQuery<T, TOptions> where T : class, ITabularRecord<T> where TOptions : TabularOptions
{
    internal static IEnumerable<T> Read(StreamSource source, TOptions options, IRecordReaderFactory<TOptions> format)
    {
        using var lease = source.Open();
        ValidateReadable(lease.Stream);
        using var reader = format.Open(lease.Stream, options);
        if (!reader.Read()) throw MissingHeader(reader.Row);
        var ordinals = SchemaBinding<T>.Bind(reader.Row, options);
        var mapper = T.Mapper;
        int count = reader.Row.Count;
        while (reader.Read())
        {
            if (reader.Row.Count != count) throw reader.CreateFieldCountException(count);
            yield return mapper.Read(reader.Row, ordinals);
        }
    }

    internal static async IAsyncEnumerable<T> ReadAsync(StreamSource source, TOptions options,
        IRecordReaderFactory<TOptions> format, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var lease = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateReadable(lease.Stream);
        await using var reader = await format.OpenAsync(lease.Stream, options, cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw MissingHeader(reader.Row);
        cancellationToken.ThrowIfCancellationRequested();
        var ordinals = SchemaBinding<T>.Bind(reader.Row, options);
        var mapper = T.Mapper;
        int count = reader.Row.Count;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Row.Count != count) throw reader.CreateFieldCountException(count);
            yield return mapper.Read(reader.Row, ordinals);
        }
    }

    private static void ValidateReadable(Stream stream)
    {
        if (!stream.CanRead) throw new InvalidOperationException("The source stream does not support reading.");
    }

    private static TabularSchemaException MissingHeader(LinqToFile.Infrastructure.TabularRow row) =>
        row.WithLocation(new TabularSchemaException("The required header is missing.", 1,
            row.WorksheetName is null ? 1 : 0, row.WorksheetName is null ? 1 : 0));
}
