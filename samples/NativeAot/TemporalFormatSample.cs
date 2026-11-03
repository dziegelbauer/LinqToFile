using System.Globalization;
using LinqToFile;
using SampleModels;

internal static class TemporalFormatSample
{
    internal static async Task RunAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        string workbookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            var file = TabularFile.Csv<Appointment>(path)
                .WithCulture(CultureInfo.GetCultureInfo("fr-FR"))
                .WithDelimiter(';');
            var appointment = new Appointment
            {
                Date = new DateOnly(2004, 11, 23),
                Start = new TimeOnly(14, 30, 59),
                RecordedAt = new DateTimeOffset(2004, 11, 23, 13, 45, 56, TimeSpan.FromMinutes(345)),
                Reminder = new DateTime(2004, 11, 23, 14, 0, 0, DateTimeKind.Unspecified)
            };
            await file.CreateAsync(new[] { appointment });
            var rows = await file.ToListAsync();
            if (rows.Count != 1 || rows[0].Date != appointment.Date || rows[0].Start != new TimeOnly(14, 30) ||
                rows[0].RecordedAt != appointment.RecordedAt || rows[0].RecordedAt.Offset != appointment.RecordedAt.Offset ||
                rows[0].Reminder != appointment.Reminder || rows[0].Reminder?.Kind != DateTimeKind.Unspecified ||
                !File.ReadAllText(path).Contains("23 novembre 2004", StringComparison.Ordinal))
                throw new InvalidOperationException("Exact temporal CSV round trip failed.");
            await file.AppendAsync(new Appointment { Date = appointment.Date, Start = appointment.Start, RecordedAt = appointment.RecordedAt });
            file.Insert(1, appointment);
            var updated = file.ToArray();
            if (updated.Length != 3 || updated[1].Reminder is null || updated[2].Reminder is not null)
                throw new InvalidOperationException("Nullable temporal append/insert failed.");

            QuickStartSample.CreateWorkbook(workbookPath);
            var native = await TabularFile.Xlsx<NativeSerialDate>(workbookPath).WithCulture(CultureInfo.GetCultureInfo("fr-FR")).ToListAsync();
            if (!native.Select(r => r.Date).SequenceEqual(new[] { new DateOnly(1900, 1, 1), new DateOnly(1900, 1, 2) }))
                throw new InvalidOperationException("A text format changed native workbook date interpretation.");
        }
        finally { File.Delete(path); File.Delete(workbookPath); }
        Console.WriteLine("Temporal formats: culture, exact text, CSV writes, nullable values, precision, and native XLSX dates passed.");
    }
}
