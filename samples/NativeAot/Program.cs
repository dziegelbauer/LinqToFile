using LinqToFile;
using SampleModels;
using System.Globalization;
using System.Text;

await CsvWritingSample.RunAsync();
await QuickStartSample.RunAsync();

// Read a separately compiled model through its generated static mapping contract.
var path = Path.GetTempFileName();
try
{
    File.WriteAllText(path, "person_id,full_name,Age,Balance\n1,Ada,36,12.50\n2,Grace,17,\n");
    var adults = TabularFile.Csv<Person>(path).Where(person => person.Age >= 18).ToList();
    if (adults.Count != 1 || adults[0].Id != 1 || adults[0].Name != "Ada" || adults[0].Balance != 12.50m)
        throw new InvalidOperationException("The sample result is incorrect.");
    Console.WriteLine($"Adults: {adults.Count}; first: {adults[0].Name}");

    File.WriteAllText(path, " person_id ; full_name ; Age ; Balance \r\n7;'  Ada;''Lovelace''  ';36;12.50\r\n");
    var configured = TabularFile.Csv<Person>(path)
        .WithDelimiter(';')
        .WithQuote('\'')
        .WithHeaderTrimming()
        .WithFieldTrimming()
        .WithBufferSize(3)
        .WithMaxFieldLength(64)
        .WithMaxRecordLength(128)
        .WithBomDetection(false)
        .WithBlankLineSkipping(false)
        .Single();
    if (configured.Id != 7 || configured.Name != "Ada;'Lovelace'" || configured.Balance != 12.50m)
        throw new InvalidOperationException("The configured CSV result is incorrect.");
    Console.WriteLine($"Configured CSV: {configured.Name}");

    File.WriteAllText(path, "UInt8,Int8,Int16,UInt16,Int32,UInt32,Int64,UInt64,Int128,UInt128,Half,Single,Double,Decimal,Character,Boolean,Id,Timestamp,TimestampOffset,Date,Time,Duration,State,Access,text\n\"255\",\"-128\",\"-32768\",\"65535\",\"-2147483648\",\"4294967295\",\"-9223372036854775808\",\"18446744073709551615\",\"-170141183460469231731687303715884105728\",\"340282366920938463463374607431768211455\",\"1.5\",\"-2.5e2\",\"1.25e2\",\"12.50\",\"雪\",\"tRuE\",\"00000000-0000-0000-0000-000000000001\",\"2026-11-01T01:30:45.1234567\",\"2026-11-01T01:30:45.1234567-07:00\",\"2026-11-01\",\"01:30:45.1234567\",\"1.02:03:04.0050000\",\"Ready\",\"Read, Write\",\" raw \"\n");
    var scalar = TabularFile.Csv<ScalarRecord>(path)
        .WithCulture(CultureInfo.InvariantCulture)
        .WithBufferSize(1)
        .Single();
    if (scalar.UInt8 != byte.MaxValue || scalar.Int8 != sbyte.MinValue ||
        scalar.Int16 != short.MinValue || scalar.UInt16 != ushort.MaxValue ||
        scalar.Int32 != int.MinValue || scalar.UInt32 != uint.MaxValue ||
        scalar.Int64 != long.MinValue || scalar.UInt64 != ulong.MaxValue ||
        scalar.Int128 != Int128.MinValue || scalar.UInt128 != UInt128.MaxValue ||
        scalar.Half != (Half)1.5 || scalar.Single != -250f || scalar.Double != 125d || scalar.Decimal != 12.50m ||
        scalar.Character != '雪' || !scalar.Boolean || scalar.Id != new Guid("00000000-0000-0000-0000-000000000001") ||
        scalar.Timestamp.Kind != DateTimeKind.Unspecified || scalar.Timestamp.Hour != 1 ||
        scalar.TimestampOffset.Offset != TimeSpan.FromHours(-7) || scalar.TimestampOffset.Hour != 1 ||
        scalar.Date != new DateOnly(2026, 11, 1) || scalar.Time.Hour != 1 ||
        scalar.Duration != new TimeSpan(1, 2, 3, 4, 5) || scalar.Name != " raw " ||
        scalar.State != SampleStatus.Ready || scalar.Access != (SamplePermission.Read | SamplePermission.Write))
        throw new InvalidOperationException("The scalar mapping result is incorrect.");
    Console.WriteLine($"Scalar mapping: {scalar.State}; offset: {scalar.TimestampOffset.Offset}");
    File.WriteAllText(path, "Value\n\"12,50\"");
    var price = TabularFile.Csv<LocalizedPrice>(path)
        .WithCulture(CultureInfo.GetCultureInfo("fr-FR"))
        .Single();
    if (price.Value != 12.50m)
        throw new InvalidOperationException("The localized conversion result is incorrect.");

    const string peopleCsv = "person_id,full_name,Age,Balance\n1,Ada,36,12.50\n2,Grace,17,\n";
    File.WriteAllText(path, peopleCsv);
    var asyncAdults = await TabularFile.Csv<Person>(path)
        .AsAsyncEnumerable()
        .Where(person => person.Age >= 18)
        .Select(person => person.Name)
        .ToListAsync();
    var asyncSource = TabularFile.CsvAsync<Person>(async token =>
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        return new MemoryStream(Encoding.UTF8.GetBytes(peopleCsv));
    });
    var dictionary = await asyncSource.ToDictionaryAsync(person => person.Id, person => person.Name);
    await using var supplied = new MemoryStream(Encoding.UTF8.GetBytes(peopleCsv));
    var suppliedRows = await TabularFile.Csv<Person>(supplied).ToArrayAsync();
    if (asyncAdults.Count != 1 || asyncAdults[0] != "Ada" || dictionary[2] != "Grace" ||
        suppliedRows.Length != 2 || !supplied.CanRead)
        throw new InvalidOperationException("The async query result is incorrect.");
    Console.WriteLine($"Async CSV: {asyncAdults.Count} adult; {dictionary.Count} dictionary entries");
    WorkbookSample.Run();
    await WorkbookAsyncSample.RunAsync();
}
finally
{
    File.Delete(path);
}
