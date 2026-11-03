using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class InventoryItem
{
    public int Id { get; init; }

    [BooleanTokens("yes", "no"), NullToken("NULL")]
    public bool? Available { get; init; }

    [NullToken("NULL")]
    public string? Note { get; init; }

    [NullToken("NULL"), TemporalFormat("yyyy-MM-dd")]
    public DateOnly? ExpiresOn { get; init; }
}

// The quick-start workbook stores person_id as native numbers, not token text.
[TabularRecord]
public sealed partial class NativeTokenNumber
{
    [Column("person_id"), NullToken("1")]
    public int? Id { get; init; }
}
