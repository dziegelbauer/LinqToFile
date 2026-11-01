namespace LinqToFile;

/// <summary>Base class for file-format, schema, and conversion errors. Raw values are omitted.</summary>
public class TabularFileException : Exception
{
    public long RecordNumber { get; }
    public long LineNumber { get; }
    public long FailureLineNumber { get; }
    public int? ColumnIndex { get; }
    public string? ColumnHeader { get; }
    public string? TargetMember { get; }

    internal TabularFileException(string message, long record, long line, long failureLine,
        int? column = null, string? header = null, string? member = null) : base(message)
    {
        RecordNumber = record;
        LineNumber = line;
        FailureLineNumber = failureLine;
        ColumnIndex = column;
        ColumnHeader = header;
        TargetMember = member;
    }
}

public sealed class CsvFormatException : TabularFileException
{
    internal CsvFormatException(string message, long record, long line, long failureLine, int? column = null, string? header = null, string? member = null)
        : base(message, record, line, failureLine, column, header, member) { }
}

public sealed class TabularSchemaException : TabularFileException
{
    internal TabularSchemaException(string message, long record, long line, long failureLine,
        int? column = null, string? header = null, string? member = null)
        : base(message, record, line, failureLine, column, header, member) { }
}

public sealed class TabularConversionException : TabularFileException
{
    internal TabularConversionException(long record, long line, long failureLine, int column, string header, string member)
        : base("A field could not be converted to the target property type.", record, line, failureLine, column, header, member) { }
}
