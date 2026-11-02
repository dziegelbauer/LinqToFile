namespace LinqToFile;

/// <summary>A serialization error. RecordNumber is the one-based input row; zero identifies model preparation.</summary>
public sealed class TabularWriteException : TabularFileException
{
    internal TabularWriteException(long record, int? column = null, string? header = null, string? member = null)
        : base("A record could not be serialized. Raw values are omitted.", record, 0, 0, column, header, member) { }
}
