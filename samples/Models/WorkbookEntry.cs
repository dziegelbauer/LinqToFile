using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class WorkbookEntry
{
    [Column("entry_id")]
    public int Id { get; init; }
    public required string Name { get; init; }
    public bool Active { get; set; }
    public decimal? Amount { get; init; }
}
