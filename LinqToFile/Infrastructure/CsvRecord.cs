using System.Globalization;
using System.Numerics;

namespace LinqToFile.Infrastructure;

/// <summary>Temporary parser storage used only by generated mappers. Do not retain this object.</summary>
public sealed class CsvRecord
{
    private char[] buffer;
    private Range[] fields = new Range[16];
    private int length;
    private int fieldStart;
    private readonly int capacityLimit;
    internal int CurrentFieldLength => length - fieldStart;
    internal bool TrimFields { get; set; }
    internal int Count { get; private set; }
    internal long RecordNumber { get; set; }
    internal long LineNumber { get; set; }
    internal long FailureLineNumber { get; set; }
    internal string[] Headers { get; set; } = [];
    internal string?[] TargetMembers { get; set; } = [];
    internal CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    internal CsvRecord(int initialCapacity, int? maxRecordLength)
    {
        capacityLimit = Math.Min(maxRecordLength ?? Array.MaxLength, Array.MaxLength);
        buffer = new char[Math.Min(initialCapacity, capacityLimit)];
    }

    internal void Reset()
    {
        length = fieldStart = Count = 0;
    }

    internal void Append(char value)
    {
        if (length == buffer.Length)
        {
            int capacity = (int)Math.Min(Math.Max(1L, (long)buffer.Length * 2), capacityLimit);
            if (capacity <= length) throw new InvalidOperationException("The record exceeds the supported buffer capacity.");
            Array.Resize(ref buffer, capacity);
        }
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
        var value = buffer.AsSpan()[fields[ordinal]];
        return TrimFields ? value.Trim() : value;
    }

    public string ReadString(int ordinal, string member) => GetField(ordinal).ToString();

    public T ReadNumber<T>(int ordinal, string member, NumberStyles styles) where T : struct, INumberBase<T>
    {
        var text = GetField(ordinal);
        if (text.IndexOf('\0') < 0 && T.TryParse(text, styles, Culture, out var value) && T.IsFinite(value)) return value;
        throw CreateConversionException(ordinal, member);
    }

    public int ReadInt32(int ordinal, string member) => ReadNumber<int>(ordinal, member, NumberStyles.Integer);

    public decimal ReadDecimal(int ordinal, string member) =>
        ReadNumber<decimal>(ordinal, member,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite);

    public bool ReadBoolean(int ordinal, string member)
    {
        var text = GetField(ordinal);
        return text.IndexOf('\0') < 0 && bool.TryParse(text, out var value)
            ? value : throw CreateConversionException(ordinal, member);
    }

    public Guid ReadGuid(int ordinal, string member) =>
        Guid.TryParse(GetField(ordinal), out var value) ? value : throw CreateConversionException(ordinal, member);

    public char ReadChar(int ordinal, string member)
    {
        var text = GetField(ordinal);
        return text.Length == 1 ? text[0] : throw CreateConversionException(ordinal, member);
    }

    public DateTime ReadDateTime(int ordinal, string member)
    {
        // AdjustToUniversal leaves offset-free input Unspecified, without assuming a local zone.
        if (DateTime.TryParse(GetField(ordinal), Culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
                out var value) && value.Kind == DateTimeKind.Unspecified)
            return value;
        throw CreateConversionException(ordinal, member);
    }

    public DateTimeOffset ReadDateTimeOffset(int ordinal, string member)
    {
        var text = GetField(ordinal);
        // Check for an explicit zone using the BCL parser, then retain the original offset.
        if (DateTime.TryParse(text, Culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
                out var marker) && marker.Kind == DateTimeKind.Utc &&
            DateTimeOffset.TryParse(text, Culture, DateTimeStyles.AllowWhiteSpaces, out var value))
            return value;
        throw CreateConversionException(ordinal, member);
    }

    public DateOnly ReadDateOnly(int ordinal, string member) =>
        DateOnly.TryParse(GetField(ordinal), Culture, DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value : throw CreateConversionException(ordinal, member);

    public TimeOnly ReadTimeOnly(int ordinal, string member) =>
        TimeOnly.TryParse(GetField(ordinal), Culture, DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value : throw CreateConversionException(ordinal, member);

    public TimeSpan ReadTimeSpan(int ordinal, string member) =>
        TimeSpan.TryParse(GetField(ordinal), Culture, out var value) ? value : throw CreateConversionException(ordinal, member);

    public TabularConversionException CreateConversionException(int ordinal, string member) =>
        new(RecordNumber, LineNumber, FailureLineNumber, ordinal + 1, Headers[ordinal], member);
}
