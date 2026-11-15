using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class ImmutableProduct
{
    [Column("product_id")]
    public int Id { get; }
    public string Name { get; }

    [TabularConstructor]
    public ImmutableProduct(int id, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        Id = id;
        Name = name;
    }
}
