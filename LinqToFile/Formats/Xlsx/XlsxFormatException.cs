namespace LinqToFile;

/// <summary>A workbook or SpreadsheetML structure could not be read.</summary>
public sealed class XlsxFormatException : TabularFileException
{
    internal XlsxFormatException(string message, long record = 0, int? column = null,
        string? header = null, string? member = null) : base(message, record, 0, 0, column, header, member) { }
}
