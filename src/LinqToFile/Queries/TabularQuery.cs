using System.Runtime.CompilerServices;
using LinqToFile.Infrastructure;
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
        int[]? ordinals = PrepareHeaderless(reader, options);
        if (!reader.Read())
        {
            if (options.BindingMode != ColumnBindingMode.Headers) yield break;
            throw MissingHeader(reader.Row);
        }
        ordinals = BindFirst(reader, options, ordinals);
        var mapper = T.Mapper;
        int count = options.BindingMode == ColumnBindingMode.SuppliedNames ? options.ColumnNames.Length : reader.Row.Count;
        if (options.BindingMode != ColumnBindingMode.Headers)
        {
            if (reader.Row.Count != count) throw reader.CreateFieldCountException(count);
            yield return mapper.Read(reader.Row, ordinals);
        }
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
        int[]? ordinals = PrepareHeaderless(reader, options);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (options.BindingMode != ColumnBindingMode.Headers) yield break;
            throw MissingHeader(reader.Row);
        }
        cancellationToken.ThrowIfCancellationRequested();
        ordinals = BindFirst(reader, options, ordinals);
        var mapper = T.Mapper;
        int count = options.BindingMode == ColumnBindingMode.SuppliedNames ? options.ColumnNames.Length : reader.Row.Count;
        if (options.BindingMode != ColumnBindingMode.Headers)
        {
            if (reader.Row.Count != count) throw reader.CreateFieldCountException(count);
            yield return mapper.Read(reader.Row, ordinals);
        }
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Row.Count != count) throw reader.CreateFieldCountException(count);
            yield return mapper.Read(reader.Row, ordinals);
        }
    }

    private static int[]? PrepareHeaderless(IRecordReader reader, TabularOptions options)
    {
        if (options.BindingMode == ColumnBindingMode.SuppliedNames)
            return HeaderlessSchema<T>.PrepareNames(reader.Row, options);
        if (options.BindingMode == ColumnBindingMode.Indexes) HeaderlessSchema<T>.ValidateIndexes();
        return null;
    }

    private static int[] BindFirst(IRecordReader reader, TabularOptions options, int[]? ordinals) =>
        options.BindingMode switch
        {
            ColumnBindingMode.Headers => SchemaBinding<T>.Bind(reader.Row, options),
            ColumnBindingMode.Indexes => HeaderlessSchema<T>.BindIndexes(reader.Row, options),
            _ => ordinals!
        };

    private static void ValidateReadable(Stream stream)
    {
        if (!stream.CanRead) throw new InvalidOperationException("The source stream does not support reading.");
    }

    private static TabularSchemaException MissingHeader(TabularRow row) =>
        row.WithLocation(new TabularSchemaException("The required header is missing.", 1,
            row.WorksheetName is null ? 1 : 0, row.WorksheetName is null ? 1 : 0));
}
