namespace LinqToFile;

/// <summary>The record terminator for newly written CSV content.</summary>
public enum LineEnding
{
    /// <summary>A carriage return followed by a line feed.</summary>
    CrLf,
    /// <summary>A line feed.</summary>
    Lf,
    /// <summary>A carriage return.</summary>
    Cr
}
