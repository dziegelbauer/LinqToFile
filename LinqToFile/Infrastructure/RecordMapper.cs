namespace LinqToFile.Infrastructure;

/// <summary>Compiler-generated mapping infrastructure; not an application configuration API.</summary>
public abstract class RecordMapper<T> where T : class
{
    public abstract ReadOnlySpan<string> Columns { get; }
    public abstract T Read(CsvRecord record, ReadOnlySpan<int> ordinals);
}
