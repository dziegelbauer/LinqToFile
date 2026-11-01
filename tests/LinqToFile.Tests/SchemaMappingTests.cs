using Xunit;

namespace LinqToFile.Tests;

public sealed class SchemaMappingTests
{
    private static TResult WithFile<TResult>(string text, Func<string, TResult> action)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, text); return action(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CaseDependentMappingConflictsFailAtConfigurationTime()
    {
        var source = TabularFile.Csv<CaseSensitiveMappingRow>("file-does-not-exist.csv");
        Assert.Throws<ArgumentException>(() => source.WithHeaderComparison(StringComparer.OrdinalIgnoreCase));
        WithFile("Value,value\n1,2", path =>
        {
            var row = TabularFile.Csv<CaseSensitiveMappingRow>(path).Single();
            Assert.Equal(1, row.First);
            Assert.Equal(2, row.Second);
            return true;
        });
    }

    [Fact]
    public void MissingHeaderReportsTargetPropertyBeforeAnyDataRow()
    {
        WithFile("Other", path =>
        {
            var error = Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<AnnotatedIdRow>(path).ToList());
            Assert.Equal("person_id", error.ColumnHeader);
            Assert.Equal("Id", error.TargetMember);
            Assert.Equal(1, error.RecordNumber);
            Assert.Null(error.ColumnIndex);
            return true;
        });
    }

    [Theory]
    [InlineData("person_id\na\"b")]
    [InlineData("person_id\n1234567890")]
    public void ParserErrorsReportMappedPropertyWhenHeaderIsKnown(string csv)
    {
        WithFile(csv, path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<AnnotatedIdRow>(path).WithMaxFieldLength(9).ToList());
            Assert.Equal("person_id", error.ColumnHeader);
            Assert.Equal("Id", error.TargetMember);
            Assert.Equal(1, error.ColumnIndex);
            return true;
        });
    }

    [Fact]
    public void WidthErrorReportsMissingMappedProperty()
    {
        WithFile("Extra,person_id\nx", path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<AnnotatedIdRow>(path).ToList());
            Assert.Equal("Id", error.TargetMember);
            Assert.Equal("person_id", error.ColumnHeader);
            Assert.Equal(2, error.ColumnIndex);
            return true;
        });
    }

    [Fact]
    public void MapsInheritedRequiredPropertiesAndMostDerivedOverride()
    {
        WithFile("Enabled,Name,record_id\ntrue,Ada,42", path =>
        {
            var row = TabularFile.Csv<DerivedMappingRow>(path).Single();
            Assert.Equal(42, row.Id);
            Assert.Equal("Ada", row.Name);
            Assert.True(row.Enabled);
            Assert.Equal("Ada", row.DisplayName);
            Assert.Equal("", row.Ignored);
            return true;
        });
    }

    [Fact]
    public void RetainedTypedRecordsSurviveLaterReadsAndIndependentEnumerations()
    {
        WithFile("person_id\n1\n2\n3", path =>
        {
            var source = TabularFile.Csv<AnnotatedIdRow>(path).WithBufferSize(1);
            var rows = source.ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Id));
            Assert.Equal(6, source.Sum(r => r.Id));
            Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Id));
            return true;
        });
    }
}

[TabularRecord]
public sealed partial class CaseSensitiveMappingRow
{
    [Column("Value")] public int First { get; init; }
    [Column("value")] public int Second { get; init; }
}

[TabularRecord]
public sealed partial class AnnotatedIdRow
{
    [Column("person_id")] public int Id { get; init; }
}

public class MappingBase
{
    [Column("base_id")] public virtual int Id { get; init; }
    public required string Name { get; init; }
    [Ignore] public string Ignored { get; set; } = "";
    public string DisplayName => Name;
}

[TabularRecord]
public sealed partial class DerivedMappingRow : MappingBase
{
    [Column("record_id")] public override int Id { get; init; }
    public bool Enabled { get; init; }
}
