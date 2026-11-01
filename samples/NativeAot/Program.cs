using LinqToFile;
using SampleModels;

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
}
finally
{
    File.Delete(path);
}
