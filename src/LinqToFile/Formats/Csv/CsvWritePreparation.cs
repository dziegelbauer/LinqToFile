using System.Text;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

internal sealed class CsvWritePreparation<T> where T : class, ITabularRecord<T>
{
    internal Encoding Encoding { get; }
    internal CsvOutput<T> Output { get; }
    internal string Separator { get; }
    internal bool? EndsWithTerminator { get; set; }

    internal CsvWritePreparation(CsvRecordReader reader, CsvOptions options, int[] order, LineEnding? ending)
    {
        Encoding = reader.Encoding;
        if (!Encoding.IsSingleByte && Encoding.CodePage is not (65001 or 1200 or 1201 or 12000 or 12001))
            throw new NotSupportedException("CSV append supports UTF-8, UTF-16, UTF-32, and stateless single-byte encodings.");
        var newLine = options.NewLine ?? ending ?? LineEnding.Lf;
        Separator = newLine switch { LineEnding.CrLf => "\r\n", LineEnding.Cr => "\r", _ => "\n" };
        Output = new(options with { NewLine = newLine }, order, reader.Row.Headers);
    }

    internal static int[] Bind(CsvRecordReader reader, CsvOptions options)
    {
        if (reader.Row.RecordNumber == 0 || reader.Row.Count == 0)
            throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        var ordinals = SchemaBinding<T>.Bind(reader.Row, options);
        if (reader.Row.Count != ordinals.Length)
        {
            int extra = Array.FindIndex(reader.Row.TargetMembers, member => member is null);
            throw new TabularSchemaException("A file header has no mapped property.", 1, reader.Row.LineNumber,
                reader.Row.FailureLineNumber, extra + 1, reader.Row.Headers[extra]);
        }
        var order = new int[ordinals.Length];
        for (int i = 0; i < ordinals.Length; i++) order[ordinals[i]] = i;
        return order;
    }

    internal static CsvWritePreparation<T> Read(Stream stream, CsvOptions options)
    {
        using var reader = new CsvRecordReader(EncodedTextReader.Open(stream, options, leaveOpen: true,
            preciseReads: !options.ValidateAppend), options);
        if (!reader.Read()) throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        int[] order = Bind(reader, options);
        LineEnding? ending;
        try { ending = reader.ReadRecordEnding(); }
        // A bare CR already terminates the header. Invalid following data belongs to the
        // skipped body in quick mode, not to the header's optional LF lookahead.
        catch (DecoderFallbackException) when (!options.ValidateAppend) { ending = LineEnding.Cr; }
        var result = new CsvWritePreparation<T>(reader, options, order, ending);
        if (options.ValidateAppend)
        {
            while (reader.Read())
                if (reader.Row.Count != order.Length) throw reader.CreateFieldCountException(order.Length);
            result.EndsWithTerminator = reader.EndsWithTerminator;
        }
        return result;
    }

    internal static async ValueTask<CsvWritePreparation<T>> ReadAsync(Stream stream, CsvOptions options, CancellationToken token)
    {
        using var reader = new CsvRecordReader(await EncodedTextReader.OpenAsync(stream, options, token,
            leaveOpen: true, preciseReads: !options.ValidateAppend).ConfigureAwait(false), options);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
            throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        int[] order = Bind(reader, options);
        LineEnding? ending;
        try { ending = await reader.ReadRecordEndingAsync(token).ConfigureAwait(false); }
        catch (DecoderFallbackException) when (!options.ValidateAppend) { ending = LineEnding.Cr; }
        var result = new CsvWritePreparation<T>(reader, options, order, ending);
        if (options.ValidateAppend)
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                if (reader.Row.Count != order.Length) throw reader.CreateFieldCountException(order.Length);
            result.EndsWithTerminator = reader.EndsWithTerminator;
        }
        return result;
    }

    // Byte comparisons avoid starting a decoder in the middle of a multibyte sequence.
    internal bool NeedsSeparator(ReadOnlySpan<byte> tail) =>
        EndsWithTerminator is bool terminated ? !terminated :
            !tail.EndsWith(Encoding.GetBytes("\r")) && !tail.EndsWith(Encoding.GetBytes("\n"));

    internal static byte[] Tail(Stream stream)
    {
        var tail = new byte[(int)Math.Min(4, stream.Length)];
        stream.Position = stream.Length - tail.Length;
        stream.ReadExactly(tail);
        return tail;
    }
    internal static async ValueTask<byte[]> TailAsync(Stream stream, CancellationToken token)
    {
        var tail = new byte[(int)Math.Min(4, stream.Length)];
        stream.Position = stream.Length - tail.Length;
        await stream.ReadExactlyAsync(tail, token).ConfigureAwait(false);
        return tail;
    }
}
