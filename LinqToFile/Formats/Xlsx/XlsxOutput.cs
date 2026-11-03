using System.Globalization;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

// Reuses scalar and active-row storage; never retains an input sequence or shared-string table.
internal sealed class XlsxOutput<T> where T : class, ITabularRecord<T>
{
    private readonly XlsxWriteOptions options;
    private readonly int[] ordinals;
    private readonly TabularValue value;
    private char[] buffer = new char[1024];
    private int length;
    private int column;
    private long inputRow;
    internal ReadOnlyMemory<char> Record => buffer.AsMemory(0, length);

    internal XlsxOutput(XlsxWriteOptions options)
    {
        this.options = options;
        value = new(options.Culture, options.TrimFields, options.DateSystem);
        var mapper = T.Mapper;
        if (!mapper.CanWrite)
        {
            int index = mapper.UnreadableMemberIndex;
            throw new TabularWriteException(0, header: index < 0 ? null : mapper.Columns[index],
                member: index < 0 ? null : mapper.Members[index]);
        }
        if (mapper.Columns.Length is < 1 or > 16384)
            throw new TabularSchemaException("Writing requires between one and 16,384 columns.", 1, 0, 0);
        ordinals = Enumerable.Range(0, mapper.Columns.Length).ToArray();
        Array.Sort(ordinals, (a, b) => StringComparer.Ordinal.Compare(T.Mapper.Columns[a], T.Mapper.Columns[b]));
        var seen = new HashSet<string>(options.HeaderComparison);
        for (column = 0; column < ordinals.Length; column++)
        {
            string name = mapper.Columns[ordinals[column]];
            string effective = options.TrimHeaders ? name.Trim() : name;
            if (string.IsNullOrWhiteSpace(effective) || !seen.Add(effective) || !options.HeaderComparison.Equals(name, effective))
                throw Failure(true);
        }
        Start(1);
        for (column = 0; column < ordinals.Length; column++)
            Cell(mapper.Columns[ordinals[column]], ExportCellKind.Text);
        Append("</row>");
    }

    internal void Row(T record, long number)
    {
        inputRow = number;
        if (record is null || number > 1_048_575) throw Located(new TabularWriteException(number));
        Start(number + 1);
        bool content = false;
        for (column = 0; column < ordinals.Length; column++)
        {
            value.Reset();
            try { T.Mapper.WriteField(record, ordinals[column], value); }
            catch (Exception error) when (error is not OutOfMemoryException) { throw Failure(false); }
            content |= !value.Text.IsEmpty;
            Cell(value.Text, value.OutputKind);
        }
        if (!content) throw Located(new TabularWriteException(number));
        Append("</row>");
    }

    private TabularFileException Failure(bool header)
    {
        int index = ordinals[column];
        return Located(header
            ? new TabularSchemaException("A generated header cannot be written with these options.", 1, 0, 0,
                column + 1, T.Mapper.Columns[index], T.Mapper.Members[index])
            : new TabularWriteException(inputRow, column + 1, T.Mapper.Columns[index], T.Mapper.Members[index]));
    }

    private TabularFileException Located(TabularFileException error)
    {
        error.WorksheetName = options.Worksheet;
        error.WorksheetRowNumber = inputRow + 1;
        error.PartName = "xl/worksheets/sheet1.xml";
        if (error.ColumnIndex is { } index) error.CellReference = Address(index, inputRow + 1);
        return error;
    }

    private static string Address(int column, long row)
    {
        Span<char> name = stackalloc char[24];
        int end = 3;
        while (column != 0) { name[--end] = (char)('A' + (column - 1) % 26); column = (column - 1) / 26; }
        row.TryFormat(name[3..], out int count, provider: CultureInfo.InvariantCulture);
        return new string(name.Slice(end, 3 - end + count));
    }

    private void Start(long row)
    {
        length = 0;
        Append("<row r=\"");
        Span<char> digits = stackalloc char[20];
        row.TryFormat(digits, out int written, provider: CultureInfo.InvariantCulture);
        Append(digits[..written]);
        Append("\">");
    }

    private void Cell(ReadOnlySpan<char> text, ExportCellKind kind)
    {
        if (text.Length > Math.Min(32767, options.MaxCellTextLength ?? 32767)) throw Failure(inputRow == 0);
        Append("<c");
        if (kind == ExportCellKind.Blank) { Append("/>"); return; }
        if (kind == ExportCellKind.Text)
        {
            Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
            Text(text);
            Append("</t></is></c>");
            return;
        }
        if (kind == ExportCellKind.Boolean) Append(" t=\"b\"");
        else if (kind == ExportCellKind.Date) Append(" s=\"1\"");
        else if (kind == ExportCellKind.DateTime) Append(" s=\"2\"");
        else if (kind == ExportCellKind.Time) Append(" s=\"3\"");
        Append("><v>"); Append(text); Append("</v></c>");
    }

    private void Text(ReadOnlySpan<char> text)
    {
        int lineFeeds = 0;
        Span<char> escaped = stackalloc char[7];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n' && ++lineFeeds > 253) throw Failure(inputRow == 0);
            if (c == '&') Append("&amp;");
            else if (c == '<') Append("&lt;");
            else if (c == '>') Append("&gt;");
            else if (c == '_' && IsEscape(text[i..])) Append("_x005F_");
            else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            { Append(text.Slice(i, 2)); i++; }
            else if ((c < ' ' && c is not ('\t' or '\n')) || char.IsSurrogate(c) || c is '\uFFFE' or '\uFFFF')
            {
                escaped[0] = '_'; escaped[1] = 'x'; escaped[6] = '_';
                ((int)c).TryFormat(escaped[2..6], out _, "X4", CultureInfo.InvariantCulture);
                Append(escaped);
            }
            else Append(text.Slice(i, 1));
        }
    }

    private static bool IsEscape(ReadOnlySpan<char> text) => text.Length >= 7 && text[1] == 'x' && text[6] == '_' &&
        char.IsAsciiHexDigit(text[2]) && char.IsAsciiHexDigit(text[3]) && char.IsAsciiHexDigit(text[4]) && char.IsAsciiHexDigit(text[5]);

    private void Append(ReadOnlySpan<char> text)
    {
        int required = checked(length + text.Length);
        if (required > buffer.Length) Array.Resize(ref buffer, Math.Max(required, checked(buffer.Length * 2)));
        text.CopyTo(buffer.AsSpan(length)); length = required;
    }
}
