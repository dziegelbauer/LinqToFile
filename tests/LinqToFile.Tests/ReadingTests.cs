using System.Text;
using LinqToFile;
using Xunit;

namespace LinqToFile.Tests;

public sealed class ReadingTests
{
    private static void WithFile(string contents, Action<string> action) =>
        WithFile(contents, path => { action(path); return true; });

    private static TResult WithFile<TResult>(string contents, Func<string, TResult> action)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, contents); return action(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MapsInitRequiredNullableAndExtraColumns()
    {
        WithFile("person_id,Name,Age,Balance,Enabled,Token,Extra\n7,Ada,36,12.50,true,00000000-0000-0000-0000-000000000001,ignored\n8,Grace,17,,FALSE,00000000-0000-0000-0000-000000000002,x",
            path =>
            {
                var rows = TabularFile.Csv<TestPerson>(path).ToList();
                Assert.Equal(2, rows.Count);
                Assert.Equal(7, rows[0].Id);
                Assert.Equal("Ada", rows[0].Name);
                Assert.Equal(12.50m, rows[0].Balance);
                Assert.True(rows[0].Enabled);
                Assert.Null(rows[1].Balance);
                Assert.False(rows[1].Enabled);
                var adults = TabularFile.Csv<TestPerson>(path).Where(p => p.Age >= 18).ToList();
                Assert.Single(adults);
                return true;
            });
    }

    [Fact]
    public void QueryIsDeferredAndEnumerationsAreIndependent()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        var source = TabularFile.Csv<NumberRow>(path);
        using var enumerator = source.GetEnumerator();
        try
        {
            File.WriteAllText(path, "Value\n1\n2");
            Assert.True(enumerator.MoveNext());
            Assert.Equal(1, enumerator.Current.Value);
            Assert.Equal(new[] { 1, 2 }, source.Select(r => r.Value));
            File.WriteAllText(path, "Value\n3");
            Assert.Equal(3, source.Single().Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void HeaderComparisonOptionsAreImmutable()
    {
        WithFile("value\n42", path =>
        {
            var original = TabularFile.Csv<NumberRow>(path);
            var configured = original.WithHeaderComparison(StringComparer.OrdinalIgnoreCase);
            Assert.Equal(42, configured.Single().Value);
            Assert.Throws<TabularSchemaException>(() => original.ToList());
            return true;
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\r\n")]
    [InlineData("Other")]
    [InlineData("Value,Value")]
    [InlineData("Value,")]
    [InlineData("Value,   ")]
    public void RejectsInvalidHeadersEvenWithoutData(string csv) =>
        WithFile(csv, path => Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<NumberRow>(path).ToList()));

    [Fact]
    public void DuplicateUnmappedHeadersRespectConfiguredComparison() =>
        WithFile("Value,Extra,EXTRA", path =>
            Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<NumberRow>(path)
                .WithHeaderComparison(StringComparer.OrdinalIgnoreCase).ToList()));

    [Fact]
    public void ValidHeaderOnlyReturnsNoRecords() =>
        WithFile("Value", path => Assert.Empty(TabularFile.Csv<NumberRow>(path)));

    [Theory]
    [InlineData("Value\n1,2")]
    [InlineData("Value,Other\n1")]
    public void RequiresExactRowWidth(string csv) =>
        WithFile(csv, path => Assert.Throws<CsvFormatException>(() => TabularFile.Csv<NumberRow>(path).ToList()));

    [Theory]
    [InlineData("Text\n\"unterminated")]
    [InlineData("Text\na\"b")]
    [InlineData("Text\n\"a\" ")]
    public void RejectsMalformedQuotes(string csv) =>
        WithFile(csv, path => Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).ToList()));

    [Fact]
    public void QuotedValuesNewlinesAndOwnedStringsSurviveBufferReuse()
    {
        var large = new string('z', 5000);
        WithFile("Text\r\n\"hello, \"\"world\"\"\r\nnext\"\r\""
            + large + "\"\n\"\"\n", path =>
        {
            var rows = TabularFile.Csv<TextRow>(path).ToArray();
            Assert.Equal("hello, \"world\"\r\nnext", rows[0].Text);
            Assert.Equal(large, rows[1].Text);
            Assert.Equal("", rows[2].Text);
            return true;
        });
    }

    [Fact]
    public void ConversionErrorHasLocationAndNoRawValue()
    {
        WithFile("\nValue\n\nsecret123", path =>
        {
            var error = Assert.Throws<TabularConversionException>(() => TabularFile.Csv<NumberRow>(path).ToList());
            Assert.Equal(2, error.RecordNumber);
            Assert.Equal(4, error.LineNumber);
            Assert.Equal(1, error.ColumnIndex);
            Assert.Equal("Value", error.ColumnHeader);
            Assert.Equal("Value", error.TargetMember);
            Assert.DoesNotContain("secret123", error.ToString());
            return true;
        });
    }

    [Fact]
    public void EncodingIsConfigurableAndInvalidUtf8Throws()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes("Text\ncafé"));
            Assert.Throws<DecoderFallbackException>(() => TabularFile.Csv<TextRow>(path).ToList());
            Assert.Equal("café", TabularFile.Csv<TextRow>(path).WithEncoding(Encoding.Latin1).Single().Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void EarlyExitReleasesFile()
    {
        WithFile("Value\n1\n2", path =>
        {
            Assert.Single(TabularFile.Csv<NumberRow>(path).Take(1));
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        });
    }

    [Fact]
    public void InvalidUtf8WithBomStillThrows()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, (byte)'T', (byte)'e', (byte)'x', (byte)'t', 10, 0xFF]);
            Assert.Throws<DecoderFallbackException>(() => TabularFile.Csv<TextRow>(path).ToList());
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public void BomOverridesConfiguredEncodingAndPreservesUnicode(string encodingName)
    {
        var path = Path.GetTempFileName();
        try
        {
            var encoding = Encoding.GetEncoding(encodingName);
            File.WriteAllBytes(path, [.. encoding.GetPreamble(), .. encoding.GetBytes("Text\n雪")]);
            Assert.Equal("雪", TabularFile.Csv<TextRow>(path).WithEncoding(Encoding.Latin1).Single().Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidOptionsFailImmediately()
    {
        Assert.Throws<ArgumentException>(() => TabularFile.Csv<NumberRow>(""));
        var source = TabularFile.Csv<NumberRow>("unused.csv");
        Assert.Throws<ArgumentNullException>(() => source.WithEncoding(null!));
        Assert.Throws<ArgumentException>(() => source.WithHeaderComparison(StringComparer.CurrentCulture));
    }

}

[TabularRecord]
public sealed partial class TestPerson
{
    [Column("person_id")] public int Id { get; init; }
    public required string Name { get; init; }
    public int Age { get; set; }
    public decimal? Balance { get; init; }
    public bool Enabled { get; init; }
    public Guid Token { get; init; }
    [Ignore] public string Ignored => Name;
}

[TabularRecord]
public sealed partial class NumberRow { public int Value { get; init; } }

[TabularRecord]
public sealed partial class TextRow { public required string Text { get; init; } }

public sealed class Unmarked { public int Value { get; set; } }
