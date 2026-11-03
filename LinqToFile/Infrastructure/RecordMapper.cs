namespace LinqToFile.Infrastructure;

/// <summary>Compiler-generated mapping infrastructure; not an application configuration API.</summary>
public abstract class RecordMapper<T> where T : class
{
    public abstract ReadOnlySpan<string> Columns { get; }
    public abstract ReadOnlySpan<string> Members { get; }
    public abstract T Read(TabularRow record, ReadOnlySpan<int> ordinals);
    public virtual void ValidateCulture(System.Globalization.CultureInfo culture) { }
    public virtual bool CanWrite => false;
    public virtual int UnreadableMemberIndex => -1;
    public virtual void WriteField(T record, int ordinal, TabularValue value) =>
        throw new NotSupportedException("This mapping does not support writing. Regenerate the model with the current generator.");
}
