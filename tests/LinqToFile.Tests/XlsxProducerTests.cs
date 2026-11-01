using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxProducerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsIndependentExcelIdentifiedSharedStringHeader(bool spill)
    {
        var rows = TabularFile.Xlsx<ProducerSharedHeader>(Fixture("SST.xlsx"))
            .WithSharedStringMemoryBudget(spill ? 0 : 1024).ToArray();
        Assert.Empty(rows);
    }

    [Fact]
    public void ReadsIndependentCachedFormulaHeaderAndNumericRows()
    {
        Assert.Empty(TabularFile.Xlsx<ProducerFormulaHeader>(Fixture("CalcChain.xlsx")));
        var values = TabularFile.Xlsx<ProducerNumber>(Fixture("CF.xlsx")).Select(row => row.Value).ToArray();
        Assert.Equal(Enumerable.Range(2, 14), values);
        Assert.Equal(119, values.Sum());
    }

    [Theory]
    [InlineData("SST.xlsx")]
    [InlineData("CalcChain.xlsx")]
    [InlineData("CF.xlsx")]
    public void FixtureApplicationMetadataIdentifiesExcel(string name)
    {
        using var archive = ZipFile.OpenRead(Fixture(name));
        using var stream = archive.GetEntry("docProps/app.xml")!.Open();
        var properties = XDocument.Load(stream);
        Assert.Equal("Microsoft Excel", properties.Root!.Element(XName.Get("Application", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"))!.Value);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Xlsx", name);
}

[TabularRecord] public sealed partial class ProducerSharedHeader { [Column("shared string table")] public string Value { get; init; } = ""; }
[TabularRecord] public sealed partial class ProducerFormulaHeader { [Column("2")] public int Value { get; init; } }
[TabularRecord] public sealed partial class ProducerNumber { [Column("1")] public int Value { get; init; } }
