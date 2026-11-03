using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class HeaderlessEntry
{
    [Column("id"), ColumnIndex(0)]
    public int Id { get; init; }

    [Column("name"), ColumnIndex(2)]
    public required string Name { get; init; }
}
