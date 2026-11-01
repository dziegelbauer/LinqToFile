namespace LinqToFile;

public sealed class CsvFormatException : TabularFileException
{
    internal CsvFormatException(string message, long record, long line, long failureLine,
        int? column = null, string? header = null, string? member = null)
        : base(message, record, line, failureLine, column, header, member) { }
}
