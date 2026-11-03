using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class Appointment
{
    [TemporalFormat("dd MMMM yyyy")]
    public DateOnly Date { get; init; }

    [TemporalFormat("HH:mm")]
    public TimeOnly Start { get; init; }

    [TemporalFormat("yyyy-MM-dd'T'HH:mm:sszzz")]
    public DateTimeOffset RecordedAt { get; init; }

    [TemporalFormat("yyyy-MM-dd HH:mm:ss")]
    public DateTime? Reminder { get; init; }
}

// Reuse the quick-start workbook's numeric fixture to test native date conversion.
[TabularRecord]
public sealed partial class NativeSerialDate
{
    [Column("person_id"), TemporalFormat("dd/MM/yyyy")]
    public DateOnly Date { get; init; }
}
