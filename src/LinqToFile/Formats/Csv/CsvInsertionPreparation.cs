using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

internal readonly record struct CsvInsertionPlan<T>(CsvWritePreparation<T> Formatting, long Offset, long OriginalEnd)
    where T : class, ITabularRecord<T>;

internal static class CsvInsertionPreparation<T> where T : class, ITabularRecord<T>
{
    private static long Complete(long position, long count, long offset, long end)
    {
        if (position > count) throw new ArgumentOutOfRangeException(nameof(position), "The insertion position exceeds the data-row count.");
        return position == count ? end : offset;
    }
    internal static CsvInsertionPlan<T> Read(Stream stream, CsvOptions options, long position)
    {
        using var text = PositionedTextReader.Open(stream, options);
        using var reader = new CsvRecordReader(text, options, text.Encoding);
        if (!reader.Read()) throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        int[] order = CsvWritePreparation<T>.Bind(reader, options);
        var formatting = new CsvWritePreparation<T>(reader, options, order, reader.ReadRecordEnding());
        long count = 0, offset = -1;
        while (reader.Read())
        {
            if (reader.Row.Count != order.Length) throw reader.CreateFieldCountException(order.Length);
            if (count == position) offset = reader.RecordByteOffset;
            count = checked(count + 1);
        }
        formatting.EndsWithTerminator = reader.EndsWithTerminator;
        return new(formatting, Complete(position, count, offset, stream.Length), stream.Length);
    }
    internal static async ValueTask<CsvInsertionPlan<T>> ReadAsync(Stream stream, CsvOptions options, long position, CancellationToken token)
    {
        using var text = await PositionedTextReader.OpenAsync(stream, options, token).ConfigureAwait(false);
        using var reader = new CsvRecordReader(text, options, text.Encoding);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
            throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        int[] order = CsvWritePreparation<T>.Bind(reader, options);
        var formatting = new CsvWritePreparation<T>(reader, options, order,
            await reader.ReadRecordEndingAsync(token).ConfigureAwait(false));
        long count = 0, offset = -1;
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (reader.Row.Count != order.Length) throw reader.CreateFieldCountException(order.Length);
            if (count == position) offset = reader.RecordByteOffset;
            count = checked(count + 1);
        }
        formatting.EndsWithTerminator = reader.EndsWithTerminator;
        return new(formatting, Complete(position, count, offset, stream.Length), stream.Length);
    }
}
