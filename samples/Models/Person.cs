using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class Person
{
    [Column("person_id")]
    public int Id { get; init; }

    [Column("full_name")]
    public required string Name { get; init; }

    public int Age { get; set; }
    public decimal? Balance { get; init; }

    [Ignore]
    public string DisplayName => Name;
}
