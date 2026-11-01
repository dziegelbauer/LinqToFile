using System.Collections;
using System.Text;
using LinqToFile.Infrastructure;

namespace LinqToFile;

public static class TabularFile
{
    public static CsvSource<T> Csv<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, new UTF8Encoding(false, true), StringComparer.Ordinal);
    }
}

/// <summary>An immutable, deferred CSV source. Each enumeration opens an independent file.</summary>
public sealed class CsvSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    private readonly Encoding encoding;
    private readonly StringComparer comparer;

    internal CsvSource(string path, Encoding encoding, StringComparer comparer)
    {
        this.path = path;
        this.encoding = encoding;
        this.comparer = comparer;
    }

    public CsvSource<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(path, (Encoding)value.Clone(), comparer);
    }

    public CsvSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        return new(path, encoding, value);
    }

    public IEnumerable<T> AsEnumerable() => this;
    public IEnumerator<T> GetEnumerator() => Read().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> Read()
    {
        var mapper = T.Mapper;
        using var stream = File.OpenRead(path);
        using var reader = CsvTextReader.Open(stream, encoding);
        var parser = new CsvParser(reader);
        if (!parser.Read()) throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        var row = parser.Record;
        var headers = new string[row.Count];
        var headerMap = new Dictionary<string, int>(row.Count, comparer);
        for (var i = 0; i < headers.Length; i++)
        {
            var header = row.GetField(i).ToString();
            if (string.IsNullOrWhiteSpace(header))
                throw new TabularSchemaException("A header is empty or whitespace-only.", 1, row.LineNumber, row.FailureLineNumber, i + 1);
            if (!headerMap.TryAdd(header, i))
                throw new TabularSchemaException("A duplicate header was found.", 1, row.LineNumber, row.FailureLineNumber, i + 1, header);
            headers[i] = header;
        }
        row.Headers = headers;
        var ordinals = Bind(mapper, headerMap, row);
        while (parser.Read())
        {
            if (row.Count != headers.Length)
                throw new CsvFormatException("The record field count differs from the header.", row.RecordNumber, row.LineNumber, row.FailureLineNumber);
            yield return mapper.Read(row, ordinals);
        }
    }

    private static int[] Bind(RecordMapper<T> mapper, Dictionary<string, int> headers, CsvRecord row)
    {
        var names = mapper.Columns;
        var ordinals = new int[names.Length];
        var used = new HashSet<string>(headers.Comparer);
        for (var i = 0; i < names.Length; i++)
        {
            if (!used.Add(names[i]))
                throw new TabularSchemaException("Multiple properties map to the same column.", 1, row.LineNumber, row.FailureLineNumber, header: names[i]);
            if (!headers.TryGetValue(names[i], out ordinals[i]))
                throw new TabularSchemaException("A mapped column is missing.", 1, row.LineNumber, row.FailureLineNumber, header: names[i]);
        }
        return ordinals;
    }
}
