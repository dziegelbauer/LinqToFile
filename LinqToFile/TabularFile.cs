using System.Collections;
using System.Text;
using LinqToFile.Infrastructure;

namespace LinqToFile;

public static class TabularFile
{
    public static CsvSource<T> Csv<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, new CsvOptions());
    }
}

/// <summary>An immutable, deferred CSV source. Each enumeration opens an independent file.</summary>
public sealed class CsvSource<T> : IEnumerable<T> where T : class, ITabularRecord<T>
{
    private readonly string path;
    private readonly CsvOptions options;

    internal CsvSource(string path, CsvOptions options)
    {
        this.path = path;
        this.options = options;
    }

    public CsvSource<T> WithEncoding(Encoding value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(path, options with { Encoding = (Encoding)value.Clone() });
    }

    public CsvSource<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        return new(path, options with { HeaderComparison = value });
    }

    public CsvSource<T> WithDelimiter(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Quote) throw new ArgumentException("Delimiter and quote characters must differ.", nameof(value));
        return new(path, options with { Delimiter = value });
    }

    public CsvSource<T> WithQuote(char value)
    {
        CsvOptions.ValidateCharacter(value, nameof(value));
        if (value == options.Delimiter) throw new ArgumentException("Quote and delimiter characters must differ.", nameof(value));
        return new(path, options with { Quote = value });
    }

    public CsvSource<T> WithBomDetection(bool enabled = true) => new(path, options with { DetectBom = enabled });
    public CsvSource<T> WithFieldTrimming(bool enabled = true) => new(path, options with { TrimFields = enabled });
    public CsvSource<T> WithHeaderTrimming(bool enabled = true) => new(path, options with { TrimHeaders = enabled });
    public CsvSource<T> WithBlankLineSkipping(bool enabled = true) => new(path, options with { SkipBlankLines = enabled });

    /// <summary>Sets the decoded-character input buffer size. The value must be positive.</summary>
    public CsvSource<T> WithBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return new(path, options with { BufferSize = size });
    }

    /// <summary>Limits unescaped UTF-16 field length before trimming, including header fields. Null removes the limit.</summary>
    public CsvSource<T> WithMaxFieldLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return new(path, options with { MaxFieldLength = length });
    }

    /// <summary>Limits decoded source UTF-16 record length, excluding its terminating newline. Null removes the limit.</summary>
    public CsvSource<T> WithMaxRecordLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return new(path, options with { MaxRecordLength = length });
    }

    public IEnumerable<T> AsEnumerable() => this;
    public IEnumerator<T> GetEnumerator() => Read().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> Read()
    {
        var mapper = T.Mapper;
        using var stream = File.OpenRead(path);
        using var reader = CsvTextReader.Open(stream, options, leaveOpen: true);
        var parser = new CsvParser(reader, options);
        if (!parser.Read()) throw new TabularSchemaException("The required header is missing.", 1, 1, 1);
        var row = parser.Record;
        row.TrimFields = options.TrimHeaders;
        var headers = new string[row.Count];
        var headerMap = new Dictionary<string, int>(row.Count, options.HeaderComparison);
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
        row.TrimFields = options.TrimFields;
        while (parser.Read())
        {
            if (row.Count != headers.Length)
            {
                int missingOrExtra = Math.Min(row.Count, headers.Length);
                string? header = missingOrExtra < headers.Length ? headers[missingOrExtra] : null;
                throw new CsvFormatException("The record field count differs from the header.",
                    row.RecordNumber, row.LineNumber, row.FailureLineNumber, missingOrExtra + 1, header);
            }
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
