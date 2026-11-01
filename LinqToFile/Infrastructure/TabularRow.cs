using System.Globalization;
using System.Numerics;
using LinqToFile.Reading;

namespace LinqToFile.Infrastructure;

/// <summary>Temporary parser storage used only by generated mappers. Do not retain this object.</summary>
public sealed class TabularRow
{
    private char[] buffer;
    private Range[] fields = new Range[16];
    private int length;
    private int fieldStart;
    private readonly int capacityLimit;
    private CellKind[]? kinds;
    private string?[]? retainedStrings;
    internal string? WorksheetName { get; set; }
    internal long? WorksheetRowNumber { get; set; }
    internal string? PartName { get; set; }
    internal int CurrentFieldLength => length - fieldStart;
    internal ReadOnlySpan<char> CurrentField => buffer.AsSpan(fieldStart, length - fieldStart);
    internal Span<char> CurrentFieldStorage => buffer.AsSpan(fieldStart, length - fieldStart);
    internal void TruncateCurrentField(int count) => length = fieldStart + count;
    internal void Append(ReadOnlySpan<char> value) { foreach (char c in value) Append(c); }
    internal NumericDateSystem DateSystem { get; set; }
    internal void ReplaceCurrentField(ReadOnlySpan<char> value)
    {
        length = fieldStart;
        foreach (char c in value) Append(c);
    }
    internal bool TrimFields { get; set; }
    internal int Count { get; private set; }
    internal long RecordNumber { get; set; }
    internal long LineNumber { get; set; }
    internal long FailureLineNumber { get; set; }
    internal string[] Headers { get; set; } = [];
    internal string?[] TargetMembers { get; set; } = [];
    internal CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    internal TabularRow(int initialCapacity, int? maxRecordLength)
    {
        capacityLimit = Math.Min(maxRecordLength ?? Array.MaxLength, Array.MaxLength);
        buffer = new char[Math.Min(initialCapacity, capacityLimit)];
    }

    internal void Reset()
    {
        length = fieldStart = Count = 0;
    }

    internal void LimitFields(int count) => Count = Math.Min(Count, count);

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
        => EndField(CellKind.Text);

    internal void EndField(CellKind kind, string? retained = null)
    {
        if (Count == fields.Length) Array.Resize(ref fields, checked(fields.Length * 2));
        if (kind != CellKind.Text && kinds is null) kinds = new CellKind[fields.Length];
        if (kinds is not null)
        {
            if (kinds.Length < fields.Length) Array.Resize(ref kinds, fields.Length);
            kinds[Count] = kind;
        }
        if (retained is not null && retainedStrings is null) retainedStrings = new string?[fields.Length];
        if (retainedStrings is not null)
        {
            if (retainedStrings.Length < fields.Length) Array.Resize(ref retainedStrings, fields.Length);
            retainedStrings[Count] = retained;
        }
        fields[Count++] = fieldStart..length;
        fieldStart = length;
    }

    public ReadOnlySpan<char> GetField(int ordinal)
    {
        if ((uint)ordinal >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(ordinal));
        var value = buffer.AsSpan()[fields[ordinal]];
        return TrimFields ? value.Trim() : value;
    }

    public string ReadString(int ordinal, string member)
    {
        var value = GetField(ordinal);
        if (kinds is not null && kinds[ordinal] == CellKind.Boolean)
            return value.SequenceEqual("true") ? "true" : "false";
        // Shared strings are already immutable owned values; reuse them when trimming did not alter the field.
        return retainedStrings?[ordinal] is string retained && retained.Length == value.Length ? retained : value.ToString();
    }
    public bool IsEmpty(int ordinal) => GetField(ordinal).IsEmpty;

    // Generated enum readers use this boundary too; native numeric/boolean cells are not text.
    public ReadOnlySpan<char> GetTextField(int ordinal, string member)
    {
        RequireKind(ordinal, member, CellKind.Text);
        return GetField(ordinal);
    }

    private void RequireKind(int ordinal, string member, CellKind allowed)
    {
        if (kinds is not null && kinds[ordinal] != CellKind.Text && kinds[ordinal] != allowed)
            throw CreateConversionException(ordinal, member);
    }

    public T ReadNumber<T>(int ordinal, string member, NumberStyles styles) where T : struct, INumberBase<T>
    {
        var text = GetField(ordinal);
        RequireKind(ordinal, member, CellKind.Number);
        bool native = kinds is not null && kinds[ordinal] == CellKind.Number;
        if (text.IndexOf('\0') < 0 && T.TryParse(text, native ? NumberStyles.Float : styles,
                native ? CultureInfo.InvariantCulture : Culture, out var value) && T.IsFinite(value)) return value;
        if (native && ExactDecimal.TryParse(text, out var exact))
        {
            if (exact.IsZero) return T.Zero;
            Span<char> normalized = stackalloc char[40];
            if (styles == NumberStyles.Integer && exact.TryInteger(out var integer) && integer.TryFormat(normalized, out int written, provider: CultureInfo.InvariantCulture) &&
                T.TryParse(normalized[..written], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
        }
        throw CreateConversionException(ordinal, member);
    }

    public int ReadInt32(int ordinal, string member) => ReadNumber<int>(ordinal, member, NumberStyles.Integer);

    public decimal ReadDecimal(int ordinal, string member) =>
        ReadNumber<decimal>(ordinal, member,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite);

    public bool ReadBoolean(int ordinal, string member)
    {
        RequireKind(ordinal, member, CellKind.Boolean);
        var text = GetField(ordinal);
        return text.IndexOf('\0') < 0 && bool.TryParse(text, out var value)
            ? value : throw CreateConversionException(ordinal, member);
    }

    public Guid ReadGuid(int ordinal, string member) =>
        Guid.TryParse(GetTextField(ordinal, member), out var value) ? value : throw CreateConversionException(ordinal, member);

    public char ReadChar(int ordinal, string member)
    {
        var text = GetTextField(ordinal, member);
        return text.Length == 1 ? text[0] : throw CreateConversionException(ordinal, member);
    }

    public DateTime ReadDateTime(int ordinal, string member)
    {
        if (kinds is not null && kinds[ordinal] == CellKind.Number)
            return DateSystem != NumericDateSystem.None && NativeTemporal.TryCalendar(GetField(ordinal), DateSystem == NumericDateSystem.Excel1904, false, out var serial)
                ? serial : throw CreateConversionException(ordinal, member);
        if (kinds is not null && kinds[ordinal] == CellKind.IsoDate)
            return NativeTemporal.TryIso(GetField(ordinal), out var iso) && iso.Offset is null ? iso.DateTime : throw CreateConversionException(ordinal, member);
        // AdjustToUniversal leaves offset-free input Unspecified, without assuming a local zone.
        if (DateTime.TryParse(GetTextField(ordinal, member), Culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
                out var value) && value.Kind == DateTimeKind.Unspecified)
            return value;
        throw CreateConversionException(ordinal, member);
    }

    public DateTimeOffset ReadDateTimeOffset(int ordinal, string member)
    {
        if (kinds is not null && kinds[ordinal] == CellKind.IsoDate)
            return NativeTemporal.TryIso(GetField(ordinal), out var iso) && !iso.DateOnly && iso.Offset is TimeSpan offset
                ? new DateTimeOffset(iso.DateTime, offset) : throw CreateConversionException(ordinal, member);
        var text = GetTextField(ordinal, member);
        // Check for an explicit zone using the BCL parser, then retain the original offset.
        if (DateTime.TryParse(text, Culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal,
                out var marker) && marker.Kind == DateTimeKind.Utc &&
            DateTimeOffset.TryParse(text, Culture, DateTimeStyles.AllowWhiteSpaces, out var value))
            return value;
        throw CreateConversionException(ordinal, member);
    }

    public DateOnly ReadDateOnly(int ordinal, string member)
    {
        if (kinds is not null && kinds[ordinal] == CellKind.Number)
            return DateSystem != NumericDateSystem.None && NativeTemporal.TryCalendar(GetField(ordinal), DateSystem == NumericDateSystem.Excel1904, true, out var serial)
                ? DateOnly.FromDateTime(serial) : throw CreateConversionException(ordinal, member);
        if (kinds is not null && kinds[ordinal] == CellKind.IsoDate)
            return NativeTemporal.TryIso(GetField(ordinal), out var iso) && iso.DateOnly
                ? DateOnly.FromDateTime(iso.DateTime) : throw CreateConversionException(ordinal, member);
        return DateOnly.TryParse(GetTextField(ordinal, member), Culture, DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value : throw CreateConversionException(ordinal, member);
    }

    public TimeOnly ReadTimeOnly(int ordinal, string member)
    {
        if (kinds is not null && kinds[ordinal] == CellKind.Number)
            return DateSystem != NumericDateSystem.None && NativeTemporal.TryTime(GetField(ordinal), out var serial) ? serial : throw CreateConversionException(ordinal, member);
        return TimeOnly.TryParse(GetTextField(ordinal, member), Culture, DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value : throw CreateConversionException(ordinal, member);
    }

    public TimeSpan ReadTimeSpan(int ordinal, string member)
    {
        if (kinds is not null && kinds[ordinal] == CellKind.Number)
            return DateSystem != NumericDateSystem.None && NativeTemporal.TryDuration(GetField(ordinal), out var serial) ? serial : throw CreateConversionException(ordinal, member);
        return TimeSpan.TryParse(GetTextField(ordinal, member), Culture, out var value) ? value : throw CreateConversionException(ordinal, member);
    }

    public TabularConversionException CreateConversionException(int ordinal, string member) =>
        WithLocation(new TabularConversionException(RecordNumber, LineNumber, FailureLineNumber, ordinal + 1,
            ordinal < Headers.Length ? Headers[ordinal] : "", member));

    internal TException WithLocation<TException>(TException error) where TException : TabularFileException
    {
        error.WorksheetName = WorksheetName;
        error.WorksheetRowNumber = WorksheetRowNumber;
        error.PartName ??= PartName;
        if (WorksheetRowNumber is long row && error.ColumnIndex is int column)
        {
            Span<char> reference = stackalloc char[32];
            int index = 8;
            int end = index;
            while (column > 0) { reference[--index] = (char)('A' + (column - 1) % 26); column = (column - 1) / 26; }
            row.TryFormat(reference[end..], out int written, provider: CultureInfo.InvariantCulture);
            error.CellReference = reference.Slice(index, end - index + written).ToString();
        }
        return error;
    }
}

internal enum CellKind : byte { Text, Number, Boolean, IsoDate }
