using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class WorkbookCells
{
    public required string Name { get; init; }
    public DateTime Posted { get; init; }
    public DateTimeOffset Recorded { get; init; }
    public TimeSpan Duration { get; init; }
    public int Count { get; init; }
    public DateOnly Date { get; init; }
    public TimeOnly Clock { get; init; }
}
