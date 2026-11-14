using System.Globalization;
using System.Text;
using LinqToFile.Infrastructure;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class TokenAliasTests
{
    private static CsvSource<T> Read<T>(string text) where T : class, ITabularRecord<T> =>
        TabularFile.Csv<T>(() => new MemoryStream(Encoding.UTF8.GetBytes("Value\n\"" + text.Replace("\"", "\"\"") + "\"\n"))).WithBufferSize(1);

    [Theory]
    [InlineData("Y", true)]
    [InlineData("1", true)]
    [InlineData("n", false)]
    [InlineData("0", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("N/A", null)]
    [InlineData("NULL", null)]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    public async Task ReadsAliasesWithSyncAsyncParity(string text, bool? expected)
    {
        Assert.Equal(expected, Read<AliasBoolean>(text).Single().Value);
        Assert.Equal(expected, (await Read<AliasBoolean>(text).ToArrayAsync())[0].Value);
    }

    [Fact]
    public void ComparisonTrimmingAndNullPriorityApplyToAliases()
    {
        Assert.Throws<TabularConversionException>(() => Read<AliasBoolean>("true").Single());
        Assert.Throws<TabularConversionException>(() => Read<AliasBoolean>("n/a").Single());
        Assert.True(Read<AliasBoolean>(" Y ").WithFieldTrimming().Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<AliasBoolean>(" Y ").Single());
        Assert.Null(Read<AliasBoolean>(" N/A ").WithFieldTrimming().Single().Value);
        Assert.Null(Read<AliasBoolean>("unknown").Single().Value);
        Assert.True(Read<AliasBoolean>("UNKNOWN").Single().Value);
        Assert.True(Read<AliasSensitiveBoolean>("Y").Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<AliasSensitiveBoolean>("y").Single());
        Assert.Null(Read<AliasText>("n/a").Single().Value);
        Assert.Equal("", Read<AliasText>("").Single().Value);
    }

    [Fact]
    public async Task XlsxTextAcceptsAliasesAndNativeCellsKeepTheirTypes()
    {
        using var text = Create(SingleColumn("Y", "str"));
        Assert.True((await TabularFile.Xlsx<AliasBoolean>(text).ToArrayAsync())[0].Value);
        using var nullText = Create(SingleColumn("unknown", "str"));
        Assert.Null(TabularFile.Xlsx<AliasBoolean>(nullText).Single().Value);
        using var boolean = Create(SingleColumn("1", "b"));
        Assert.True(TabularFile.Xlsx<AliasBoolean>(boolean).Single().Value);
        using var numeric = Create(SingleColumn("1"));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<AliasBoolean>(numeric).Single());
        using var number = Create(SingleColumn("0"));
        Assert.Equal(0, TabularFile.Xlsx<AliasNumber>(number).Single().Value);
        Assert.Null(Read<AliasNumber>("0").Single().Value);
    }

    [Fact]
    public async Task WritesCanonicalTokensAndRejectsNullAliasCollisions()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<AliasBoolean>(path);
            await file.CreateAsync(new[] { new AliasBoolean { Value = true }, new AliasBoolean { Value = false }, new AliasBoolean() });
            Assert.Equal("Value\nyes\nno\nNULL\n", await File.ReadAllTextAsync(path));
            File.Delete(path);
            var text = TabularFile.Csv<AliasText>(path).WithFieldTrimming();
            text.Create(new[] { new AliasText { Value = "original" } });
            byte[] before = await File.ReadAllBytesAsync(path);
            var error = await Assert.ThrowsAsync<TabularWriteException>(() => text.AppendAsync(new AliasText { Value = " n/a " }).AsTask());
            Assert.Equal("Value", error.TargetMember);
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("n/a", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await File.ReadAllBytesAsync(path));
            File.Delete(path);
            Assert.Throws<TabularWriteException>(() => TabularFile.Csv<AliasNumber>(path).Create(new[] { new AliasNumber { Value = 0 } }));
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GeneratedAliasMappingAddsOnlyTheModelAllocation()
    {
        var row = new TabularRow(16, null);
        row.Append("Y"); row.EndField();
        var mapper = GetMapper<AliasBoolean>();
        int[] ordinals = [0];
        var output = new TabularValue(CultureInfo.InvariantCulture, true);
        for (int i = 0; i < 20000; i++) mapper.WriteField(mapper.Read(row, ordinals), 0, output);
        long minimum = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) GC.KeepAlive(new AliasBoolean());
            long models = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) mapper.WriteField(mapper.Read(row, ordinals), 0, output);
            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before - models);
        }
        Assert.Equal(0, minimum);
        Assert.Equal("yes", output.Text.ToString());
    }

    private static RecordMapper<T> GetMapper<T>() where T : class, ITabularRecord<T> => T.Mapper;
}

[TabularRecord]
public sealed partial class AliasBoolean
{
    [BooleanTokens("yes", "no", TrueAliases = new[] { "y", "1", "unknown" }, FalseAliases = new[] { "n", "0" })]
    [NullToken("NULL", Aliases = new[] { "N/A", "unknown" })]
    public bool? Value { get; init; }
}

[TabularRecord]
public sealed partial class AliasSensitiveBoolean
{
    [BooleanTokens("yes", "no", TrueAliases = new[] { "Y" }, Comparison = StringComparison.Ordinal)]
    public bool Value { get; init; }
}

[TabularRecord]
public sealed partial class AliasText
{
    [NullToken("NULL", Aliases = new[] { "N/A" }, Comparison = StringComparison.OrdinalIgnoreCase)]
    public string? Value { get; init; }
}

[TabularRecord]
public sealed partial class AliasNumber
{
    [NullToken("NULL", Aliases = new[] { "0" })]
    public int? Value { get; init; }
}
