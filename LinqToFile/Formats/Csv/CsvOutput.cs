using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Csv;

// One reusable record and scalar buffer. Input sequences are never retained.
internal sealed class CsvOutput<T> where T : class, ITabularRecord<T>
{
    private readonly CsvOptions options;
    private readonly int[] ordinals;
    private readonly TabularValue value;
    private readonly string[]? headers;
    private char[] buffer = new char[256];
    private int length;
    private int column;
    private long inputRow;
    private bool header;
    internal ReadOnlyMemory<char> Record => buffer.AsMemory(0, length);

    internal CsvOutput(CsvOptions options, int[]? fieldOrder = null, string[]? headers = null)
    {
        this.options = options;
        this.headers = headers;
        value = new(options.Culture);
        var mapper = T.Mapper;
        if (!mapper.CanWrite)
        {
            int index = mapper.UnreadableMemberIndex;
            throw new TabularWriteException(0, header: index < 0 ? null : mapper.Columns[index],
                member: index < 0 ? null : mapper.Members[index]);
        }
        if (mapper.Columns.IsEmpty) throw new TabularSchemaException("Writing requires at least one mapped column.", 1, 1, 1);
        if (fieldOrder is not null) { ordinals = fieldOrder; return; }
        ordinals = Enumerable.Range(0, mapper.Columns.Length).ToArray();
        Array.Sort(ordinals, (a, b) => StringComparer.Ordinal.Compare(T.Mapper.Columns[a], T.Mapper.Columns[b]));
        var seen = new HashSet<string>(options.HeaderComparison);
        foreach (int index in ordinals)
        {
            string name = mapper.Columns[index];
            string effective = options.TrimHeaders ? name.Trim() : name;
            if (string.IsNullOrWhiteSpace(effective) || !seen.Add(effective) || !options.HeaderComparison.Equals(name, effective))
                throw new TabularSchemaException("Generated headers cannot be read with the configured mapping.", 1, 1, 1,
                    header: name, member: mapper.Members[index]);
        }
        Header();
    }

    private void Start(bool isHeader, long row)
    {
        length = 0; column = 0; inputRow = row; header = isHeader;
    }

    private void Fail()
    {
        int index = ordinals[column];
        if (header) throw new TabularSchemaException("A generated header exceeds an output limit.", 1, 1, 1,
            column + 1, T.Mapper.Columns[index], T.Mapper.Members[index]);
        throw new TabularWriteException(inputRow, column + 1, headers?[column] ?? T.Mapper.Columns[index], T.Mapper.Members[index]);
    }

    private void Append(char c)
    {
        if (options.MaxRecordLength is int limit && length >= limit) Fail();
        AppendUnchecked(c);
    }
    private void AppendUnchecked(char c)
    {
        if (length == buffer.Length) Array.Resize(ref buffer, checked(buffer.Length * 2));
        buffer[length++] = c;
    }
    private void Field(ReadOnlySpan<char> text)
    {
        if (options.MaxFieldLength is int limit && text.Length > limit) Fail();
        if (column != 0) Append(options.Delimiter);
        bool quote = (ordinals.Length == 1 && text.IsEmpty) ||
            text.IndexOfAny(options.Delimiter, options.Quote) >= 0 || text.IndexOfAny('\r', '\n') >= 0;
        if (quote) Append(options.Quote);
        foreach (char c in text)
        {
            if (quote && c == options.Quote) Append(options.Quote);
            Append(c);
        }
        if (quote) Append(options.Quote);
    }
    private void End()
    {
        var ending = options.NewLine ?? LineEnding.Lf;
        if (ending is LineEnding.Cr or LineEnding.CrLf) AppendUnchecked('\r');
        if (ending is LineEnding.Lf or LineEnding.CrLf) AppendUnchecked('\n');
    }
    internal void Header()
    {
        Start(true, 0);
        for (; column < ordinals.Length; column++) Field(T.Mapper.Columns[ordinals[column]]);
        End();
    }
    internal void Row(T record, long number)
    {
        if (record is null) throw new TabularWriteException(number);
        Start(false, number);
        for (; column < ordinals.Length; column++)
        {
            int index = ordinals[column];
            value.Reset();
            try { T.Mapper.WriteField(record, index, value); }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Getter/format exceptions can contain sensitive data. Do not retain an inner exception.
                throw new TabularWriteException(number, column + 1, headers?[column] ?? T.Mapper.Columns[index], T.Mapper.Members[index]);
            }
            Field(value.Text);
        }
        End();
    }
}
