namespace LinqToFile;

/// <summary>Base class for file-format, schema, and conversion errors. Raw values are omitted.</summary>
public class TabularFileException : Exception
{
    /// <summary>Gets the one-based logical record number, including the header; zero identifies preparation without a record.</summary>
    public long RecordNumber { get; }
    /// <summary>Gets the starting physical CSV line, or zero when not applicable.</summary>
    public long LineNumber { get; }
    /// <summary>Gets the physical CSV line at failure, or zero when not applicable.</summary>
    public long FailureLineNumber { get; }
    /// <summary>Gets the one-based column index, or null when unavailable.</summary>
    public int? ColumnIndex { get; }
    /// <summary>Gets the column header, or null when unavailable.</summary>
    public string? ColumnHeader { get; }
    /// <summary>Gets the target property name, or null when unavailable.</summary>
    public string? TargetMember { get; }
    /// <summary>Gets the worksheet name, or null when unavailable.</summary>
    public string? WorksheetName { get; internal set; }
    /// <summary>Gets the one-based physical worksheet row, or null when unavailable.</summary>
    public long? WorksheetRowNumber { get; internal set; }
    /// <summary>Gets the worksheet cell reference, or null when unavailable.</summary>
    public string? CellReference { get; internal set; }
    /// <summary>Gets the workbook package part name, or null when unavailable.</summary>
    public string? PartName { get; internal set; }

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

/// <summary>A required header or model-to-column mapping is invalid.</summary>
public sealed class TabularSchemaException : TabularFileException
{
    internal TabularSchemaException(string message, long record, long line, long failureLine,
        int? column = null, string? header = null, string? member = null)
        : base(message, record, line, failureLine, column, header, member) { }
}

/// <summary>A field cannot be converted to its mapped property type. Raw values are omitted.</summary>
public sealed class TabularConversionException : TabularFileException
{
    internal TabularConversionException(long record, long line, long failureLine, int column, string? header, string member)
        : base("A field could not be converted to the target property type.", record, line, failureLine, column, header, member) { }
}
