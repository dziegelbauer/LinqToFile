namespace LinqToFile.Infrastructure;

/// <summary>Compiler-generated mapping infrastructure; not an application configuration API.</summary>
public abstract class RecordMapper<T> where T : class
{
    public abstract ReadOnlySpan<string> Columns { get; }
    public abstract ReadOnlySpan<string> Members { get; }
    public abstract T Read(TabularRow record, ReadOnlySpan<int> ordinals);
}
