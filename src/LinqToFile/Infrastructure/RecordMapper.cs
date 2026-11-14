namespace LinqToFile.Infrastructure;

/// <summary>Compiler-generated mapping infrastructure; not an application configuration API.</summary>
/// <typeparam name="T">The mapped record type.</typeparam>
public abstract class RecordMapper<T> where T : class
{
    /// <summary>Gets mapped column names in mapper order.</summary>
    public abstract ReadOnlySpan<string> Columns { get; }
    /// <summary>Gets target property names in mapper order.</summary>
    public abstract ReadOnlySpan<string> Members { get; }
    /// <summary>Gets headerless field positions, or an empty span when index mapping is unavailable.</summary>
    public virtual ReadOnlySpan<int> ColumnIndexes => [];
    /// <summary>Creates a mapped record from the current parser row.</summary>
    /// <param name="record">The parser row. Do not retain this object.</param>
    /// <param name="ordinals">Source field positions in mapper order.</param>
    /// <returns>The mapped record with owned values.</returns>
    public abstract T Read(TabularRow record, ReadOnlySpan<int> ordinals);
    /// <summary>Validates the culture against the generated mapping configuration.</summary>
    /// <param name="culture">The conversion culture.</param>
    public virtual void ValidateCulture(System.Globalization.CultureInfo culture) { }
    /// <summary>Gets whether all mapped properties can be read for serialization.</summary>
    public virtual bool CanWrite => false;
    /// <summary>Gets the first member without a readable getter, or -1 when all members are readable.</summary>
    public virtual int UnreadableMemberIndex => -1;
    /// <summary>Serializes one mapped property into reusable field storage.</summary>
    /// <param name="record">The record to write.</param>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="value">The output storage. Do not retain this object.</param>
    public virtual void WriteField(T record, int ordinal, TabularValue value) =>
        throw new NotSupportedException("This mapping does not support writing. Regenerate the model with the current generator.");
}
