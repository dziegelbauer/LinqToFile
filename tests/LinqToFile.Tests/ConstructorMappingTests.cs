using System.Text;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class ConstructorMappingTests
{
    private static CsvSource<ConstructorEntry> Read(string csv) =>
        TabularFile.Csv<ConstructorEntry>(() => new MemoryStream(Encoding.UTF8.GetBytes(csv))).WithBufferSize(1);

    [Fact]
    public async Task CsvReadsImmutableValuesAndInitializesRemainingProperties()
    {
        const string csv = "identifier,Name,Active,Date,Note,Extra\n7,Ada,yes,2026-11-14,NULL,9\n8,Grace,no,2026-11-15,text,10\n";
        var source = Read(csv);
        var rows = source.ToArray();
        var asyncRows = await source.ToArrayAsync();
        Assert.Equal(7, rows[0].Id);
        Assert.Equal("Ada", rows[0].Name);
        Assert.True(rows[0].Active);
        Assert.Equal(new DateOnly(2026, 11, 14), rows[0].Date);
        Assert.Null(rows[0].Note);
        Assert.Equal(9, rows[0].Extra);
        Assert.Equal("Grace", asyncRows[1].Name);
        Assert.False(asyncRows[1].Active);
        Assert.Equal("text", asyncRows[1].Note);
        Assert.Equal("Ada", rows[0].Name);
    }

    [Fact]
    public async Task XlsxReadsConstructorArgumentsWithSharedConversionRules()
    {
        string rows = Row(1, Text("A1", "identifier"), Text("B1", "Name"), Text("C1", "Active"), Text("D1", "Date"), Text("E1", "Note"), Text("F1", "Extra")) +
            Row(2, Number("A2", "7"), Text("B2", "Ada"), Cell("C2", "b", "1"), Cell("D2", "d", "2026-11-14"), Text("E2", "NULL"), Number("F2", "9"));
        var source = TabularFile.Xlsx<ConstructorEntry>(() => Create(rows));
        var row = source.Single();
        var asyncRow = Assert.Single(await source.ToArrayAsync());
        Assert.Equal(7, row.Id);
        Assert.Equal("Ada", asyncRow.Name);
        Assert.True(row.Active);
        Assert.Equal(new DateOnly(2026, 11, 14), row.Date);
        Assert.Null(row.Note);
        Assert.Equal(9, row.Extra);
    }

    [Fact]
    public async Task ImmutableModelsRoundTripThroughBothWriters()
    {
        var expected = new ConstructorEntry("Ada", 7, true, new DateOnly(2026, 11, 14), null) { Extra = 9 };
        using var csv = new MemoryStream();
        await TabularFile.CsvWriter<ConstructorEntry>(csv).WriteAsync(new[] { expected });
        csv.Position = 0;
        var csvRow = TabularFile.Csv<ConstructorEntry>(csv).Single();
        Assert.Equal(expected.Id, csvRow.Id);
        Assert.Equal(expected.Extra, csvRow.Extra);
        using var xlsx = new MemoryStream();
        await TabularFile.XlsxWriter<ConstructorEntry>(xlsx).WriteAsync(new[] { expected });
        xlsx.Position = 0;
        var xlsxRow = TabularFile.Xlsx<ConstructorEntry>(xlsx).Single();
        Assert.Equal(expected.Name, xlsxRow.Name);
        Assert.Equal(expected.Date, xlsxRow.Date);
        Assert.Null(xlsxRow.Note);
    }

    [Fact]
    public void HeaderlessIndexesAndConversionLocationsUsePropertyMetadata()
    {
        using var valid = new MemoryStream("7\n"u8.ToArray());
        Assert.Equal(7, TabularFile.Csv<ConstructorValue>(valid).WithColumnIndexes().Single().Value);
        using var invalid = new MemoryStream("Value\nsecret-invalid\n"u8.ToArray());
        var error = Assert.Throws<TabularConversionException>(() => TabularFile.Csv<ConstructorValue>(invalid).Single());
        Assert.Equal("Value", error.TargetMember);
        Assert.DoesNotContain("secret-invalid", error.Message);
    }

    [Fact]
    public async Task ConstructorArgumentsReuseCustomNullableAndEnumConversions()
    {
        var source = TabularFile.Csv<ConstructorConverted>(() =>
            new MemoryStream("Value,Count,State\n12.50,,Done\n"u8.ToArray()));
        var row = Assert.Single(await source.ToArrayAsync());
        Assert.Equal(12.50m, row.Value.Value);
        Assert.Null(row.Count);
        Assert.Equal(TokenState.Done, row.State);
    }

    [Fact]
    public void ConvertsRemainingPropertiesBeforeCallingApplicationConstructor()
    {
        using var input = new MemoryStream("Value,Last\n7,invalid\n"u8.ToArray());
        Assert.Throws<TabularConversionException>(() => TabularFile.Csv<ConstructorGuard>(input).Single());
    }

    [Fact]
    public void ConstructorBindingAllocatesNoMoreThanInitializerMapping()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Value\n" + string.Concat(Enumerable.Repeat("42\n", 100_000)));
        var constructed = TabularFile.Csv<ConstructorValue>(() => new MemoryStream(bytes));
        var initialized = TabularFile.Csv<AllocationRow>(() => new MemoryStream(bytes));
        Scan(constructed);
        Scan(initialized);
        long constructorBytes = long.MaxValue, initializerBytes = long.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            constructorBytes = Math.Min(constructorBytes, Scan(constructed));
            initializerBytes = Math.Min(initializerBytes, Scan(initialized));
        }
        Assert.InRange(constructorBytes - initializerBytes, -4096, 4096);
    }

    private static long Scan<T>(IEnumerable<T> source)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var row in source) GC.KeepAlive(row);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void ApplicationConstructorExceptionsPropagateUnchanged()
    {
        using var input = new MemoryStream("Value\n-1\n"u8.ToArray());
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => TabularFile.Csv<ConstructorValue>(input).Single());
        Assert.Equal("value", error.ParamName);
    }
}

[TabularRecord]
public sealed partial class ConstructorEntry
{
    [Column("identifier")] public int Id { get; }
    public string Name { get; }
    [BooleanTokens("yes", "no")] public bool Active { get; }
    [TemporalFormat("yyyy-MM-dd")] public DateOnly Date { get; }
    [NullToken("NULL")] public string? Note { get; }
    public required int Extra { get; init; }

    [TabularConstructor]
    public ConstructorEntry(string name, int id, bool active, DateOnly date, string? note)
    {
        Name = name;
        Id = id;
        Active = active;
        Date = date;
        Note = note;
    }
}

[TabularRecord]
public sealed partial class ConstructorValue
{
    [ColumnIndex(0)] public int Value { get; }

    [TabularConstructor]
    public ConstructorValue(int value = 42)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }
}

[TabularRecord]
public sealed partial class ConstructorConverted
{
    [ColumnConverter(typeof(AmountConverter))] public Amount Value { get; }
    public int? Count { get; }
    public TokenState State { get; }

    [TabularConstructor]
    public ConstructorConverted(Amount value, int? count, TokenState state)
    {
        Value = value;
        Count = count;
        State = state;
    }
}

[TabularRecord]
public sealed partial class ConstructorGuard
{
    public int Value { get; }
    public int Last { get; init; }

    [TabularConstructor]
    public ConstructorGuard(int value)
    {
        Value = value;
        throw new InvalidOperationException("application constructor");
    }
}
