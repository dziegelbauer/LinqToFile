using System.Globalization;

namespace LinqToFile.Infrastructure;

/// <summary>Temporary parser storage used only by generated mappers. Do not retain this object.</summary>
public sealed class CsvRecord
{
    private char[] buffer = new char[1024];
    private Range[] fields = new Range[16];
    private int length;
    private int fieldStart;
    internal int Count { get; private set; }
    internal long RecordNumber { get; set; }
    internal long LineNumber { get; set; }
    internal long FailureLineNumber { get; set; }
    internal string[] Headers { get; set; } = [];

    internal void Reset()
    {
        length = fieldStart = Count = 0;
    }

    internal void Append(char value)
    {
        if (length == buffer.Length) Array.Resize(ref buffer, checked(buffer.Length * 2));
        buffer[length++] = value;
    }

    internal void EndField()
    {
        if (Count == fields.Length) Array.Resize(ref fields, checked(fields.Length * 2));
        fields[Count++] = fieldStart..length;
        fieldStart = length;
    }

    public ReadOnlySpan<char> GetField(int ordinal)
    {
        if ((uint)ordinal >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(ordinal));
        return buffer.AsSpan()[fields[ordinal]];
    }

    public string ReadString(int ordinal, string member) => GetField(ordinal).ToString();

    public int ReadInt32(int ordinal, string member) =>
        int.TryParse(GetField(ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : throw ConversionError(ordinal, member);

    public decimal ReadDecimal(int ordinal, string member) =>
        decimal.TryParse(GetField(ordinal),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out var value) ? value : throw ConversionError(ordinal, member);

    public bool ReadBoolean(int ordinal, string member) =>
        bool.TryParse(GetField(ordinal), out var value) ? value : throw ConversionError(ordinal, member);

    public Guid ReadGuid(int ordinal, string member) =>
        Guid.TryParse(GetField(ordinal), out var value) ? value : throw ConversionError(ordinal, member);

    private TabularConversionException ConversionError(int ordinal, string member) =>
        new(RecordNumber, LineNumber, FailureLineNumber, ordinal + 1, Headers[ordinal], member);
}
