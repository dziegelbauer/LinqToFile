using System.Text;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvOptionsTests
{
    private static TResult WithFile<TResult>(string text, Func<string, TResult> action)
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, text); return action(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ComposesImmutableOptionsWithCustomQuotingAndDelimiter()
    {
        WithFile("\n Text ;Extra\r\n'  a;''b''\r\nc  ';unused\r\n", path =>
        {
            var original = TabularFile.Csv<TextRow>(path);
            var source = original.WithDelimiter(';').WithQuote('\'')
                .WithHeaderTrimming().WithFieldTrimming().WithBufferSize(1)
                .WithBomDetection(false).WithBlankLineSkipping()
                .WithMaxFieldLength(32).WithMaxRecordLength(64);
            Assert.Equal("a;'b'\r\nc", source.Single().Text);
            Assert.Throws<TabularSchemaException>(() => original.ToList());
            return true;
        });
    }

    [Fact]
    public void TabCanBeADelimiter()
    {
        WithFile("Text\tExtra\na,b\tx", path =>
        {
            Assert.Equal("a,b", TabularFile.Csv<TextRow>(path).WithDelimiter('\t').Single().Text);
            return true;
        });
    }

    [Fact]
    public void NonAsciiDelimiterAndQuoteWorkWithSingleCharacterBuffers()
    {
        WithFile("Text☃Extra\n§snow☃§§fall§;x☃ignored", path =>
        {
            // Text after a closing quote is rejected even with a multibyte quote character.
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path)
                .WithDelimiter('☃').WithQuote('§').WithBufferSize(1).ToList());
            return true;
        });
        WithFile("Text☃Extra\n§snow☃§§fall§☃ignored", path =>
        {
            Assert.Equal("snow☃§fall", TabularFile.Csv<TextRow>(path)
                .WithDelimiter('☃').WithQuote('§').WithBufferSize(1).Single().Text);
            return true;
        });
    }

    [Fact]
    public void TrimmingUsesUnicodeWhitespaceForQuotedAndUnquotedFields()
    {
        WithFile("Text\n\" \u00a0snow\u2000 \"\n \u2000plain\u00a0 \n   \n\"\"", path =>
        {
            var source = TabularFile.Csv<TextRow>(path);
            Assert.Equal(new[] { " \u00a0snow\u2000 ", " \u2000plain\u00a0 ", "   ", "" }, source.Select(r => r.Text));
            Assert.Equal(new[] { "snow", "plain", "", "" }, source.WithFieldTrimming().Select(r => r.Text));
            Assert.Equal(" \u00a0snow\u2000 ", source.WithFieldTrimming().WithFieldTrimming(false).First().Text);
            return true;
        });
    }

    [Fact]
    public void HeaderAndFieldTrimmingAreIndependent()
    {
        WithFile(" Text \n value ", path =>
        {
            var source = TabularFile.Csv<TextRow>(path);
            Assert.Throws<TabularSchemaException>(() => source.WithFieldTrimming().ToList());
            Assert.Equal(" value ", source.WithHeaderTrimming().Single().Text);
            Assert.Equal("value", source.WithHeaderTrimming().WithFieldTrimming().Single().Text);
            Assert.Throws<TabularSchemaException>(() => source.WithHeaderTrimming().WithHeaderTrimming(false).ToList());
            Assert.Equal(" value ", TabularFile.Csv<LiteralHeaderRow>(path).Single().Value);
            Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<LiteralHeaderRow>(path).WithHeaderTrimming().ToList());
            return true;
        });
    }

    [Fact]
    public void TrimmingPrecedesDuplicateHeaderDetection()
    {
        WithFile("Text, Extra ,extra", path =>
        {
            Assert.Empty(TabularFile.Csv<TextRow>(path).WithHeaderComparison(StringComparer.OrdinalIgnoreCase));
            var error = Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<TextRow>(path)
                .WithHeaderComparison(StringComparer.OrdinalIgnoreCase).WithHeaderTrimming().ToList());
            Assert.Equal(3, error.ColumnIndex);
            return true;
        });
    }

    [Theory]
    [InlineData("Value\n   ")]
    [InlineData("Value\n\" \u00a0 \"")]
    public void TrimmingMakesWhitespaceOnlyNullableValuesNull(string csv)
    {
        WithFile(csv, path =>
        {
            var source = TabularFile.Csv<NullableNumberRow>(path);
            Assert.Throws<TabularConversionException>(() => source.ToList());
            Assert.Null(source.WithFieldTrimming().Single().Value);
            return true;
        });
    }

    [Fact]
    public void FieldTrimmingDoesNotRelaxQuoteRules()
    {
        WithFile("Text\n\"x\" ", path =>
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithFieldTrimming().ToList()));
    }

    [Theory]
    [InlineData("\nText\nx", 1, 1)]
    [InlineData("Text\n\nx", 2, 2)]
    [InlineData("Text\nx\n\r\n", 3, 3)]
    public void BlankLineRejectionHasLogicalAndPhysicalLocation(string csv, int record, int line)
    {
        WithFile(csv, path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithBlankLineSkipping(false).ToList());
            Assert.Equal(record, error.RecordNumber);
            Assert.Equal(line, error.LineNumber);
            return true;
        });
    }

    [Fact]
    public void WhitespaceLinesAndQuotedEmptyFieldsRemainRecords()
    {
        WithFile("Text\n   \n\"\"\n", path =>
        {
            Assert.Equal(new[] { "   ", "" }, TabularFile.Csv<TextRow>(path).WithBlankLineSkipping(false).Select(r => r.Text));
            return true;
        });
    }

    [Fact]
    public void LimitCountsUnescapedFieldBeforeTrimmingAndCanBeRemoved()
    {
        WithFile("Text\n\"a\"\"b\"\"c\"", path =>
        {
            var source = TabularFile.Csv<TextRow>(path);
            Assert.Equal("a\"b\"c", source.WithMaxFieldLength(5).Single().Text);
            Assert.Throws<CsvFormatException>(() => source.WithMaxFieldLength(4).ToList());
            Assert.Equal("a\"b\"c", source.WithMaxFieldLength(4).WithMaxFieldLength(null).Single().Text);
            return true;
        });
        WithFile("Text\n\"  x  \"", path =>
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithFieldTrimming().WithMaxFieldLength(4).ToList()));
    }

    [Fact]
    public void FieldLimitAppliesToUnmappedColumns()
    {
        WithFile("Text,X\nok,secret", path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithMaxFieldLength(4).ToList());
            Assert.Equal(2, error.ColumnIndex);
            Assert.Equal("X", error.ColumnHeader);
            Assert.DoesNotContain("secret", error.ToString());
            return true;
        });
    }

    [Fact]
    public void RecordLimitIncludesQuotesDelimitersAndEmbeddedCrLfButNotTerminatingNewline()
    {
        const string csv = "Text,X\r\n\"a\r\nb\",x\r\n";
        WithFile(csv, path =>
        {
            var source = TabularFile.Csv<TextRow>(path).WithBufferSize(1);
            Assert.Equal("a\r\nb", source.WithMaxRecordLength(8).Single().Text);
            var error = Assert.Throws<CsvFormatException>(() => source.WithMaxRecordLength(7).ToList());
            Assert.Equal(2, error.RecordNumber);
            Assert.Equal(2, error.LineNumber);
            Assert.Equal(3, error.FailureLineNumber);
            Assert.Equal(2, error.ColumnIndex);
            Assert.Equal("X", error.ColumnHeader);
            Assert.Equal("a\r\nb", source.WithMaxRecordLength(7).WithMaxRecordLength(null).Single().Text);
            return true;
        });
    }

    [Fact]
    public void LimitsAlsoApplyToHeadersAndZeroIsAValidLimit()
    {
        WithFile("Text", path =>
        {
            Assert.Empty(TabularFile.Csv<TextRow>(path).WithMaxFieldLength(4).WithMaxRecordLength(4));
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithMaxFieldLength(3).ToList());
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithMaxRecordLength(3).ToList());
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithMaxFieldLength(0).ToList());
            Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithMaxRecordLength(0).ToList());
            return true;
        });
    }

    [Fact]
    public void MalformedMultilineRecordReportsStartAndFailureLine()
    {
        WithFile("\nText\r\n\r\n\"a\r\nb\"secret", path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).WithBufferSize(1).ToList());
            Assert.Equal(2, error.RecordNumber);
            Assert.Equal(4, error.LineNumber);
            Assert.Equal(5, error.FailureLineNumber);
            Assert.Equal(1, error.ColumnIndex);
            Assert.Equal("Text", error.ColumnHeader);
            Assert.DoesNotContain("secret", error.ToString());
            return true;
        });
    }

    [Theory]
    [InlineData("Text,Extra\nx", 2, "Extra")]
    [InlineData("Text\nx,y", 2, null)]
    public void WidthErrorsIdentifyFirstMissingOrExtraColumn(string csv, int column, string? header)
    {
        WithFile(csv, path =>
        {
            var error = Assert.Throws<CsvFormatException>(() => TabularFile.Csv<TextRow>(path).ToList());
            Assert.Equal(column, error.ColumnIndex);
            Assert.Equal(header, error.ColumnHeader);
            return true;
        });
    }

    [Fact]
    public void ClonesCallerEncodingBeforeItCanBeMutated()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [.. "Text\n"u8, 0xFF]);
            var encoding = (Encoding)new UTF8Encoding(false, true).Clone();
            var source = TabularFile.Csv<TextRow>(path).WithEncoding(encoding);
            encoding.DecoderFallback = DecoderFallback.ReplacementFallback;
            Assert.Throws<DecoderFallbackException>(() => source.ToList());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PublicBomOptionDisablesSwitchingButStillRemovesMatchingBom()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("Text\n雪")]);
            var source = TabularFile.Csv<TextRow>(path).WithEncoding(Encoding.Latin1);
            Assert.Equal("雪", source.Single().Text);
            Assert.Throws<TabularSchemaException>(() => source.WithBomDetection(false).ToList());
            Assert.Equal("雪", source.WithBomDetection(false).WithBomDetection().Single().Text);
            File.WriteAllBytes(path, [.. Encoding.UTF8.GetPreamble(), .. "Text\nsnow"u8]);
            Assert.Equal("snow", TabularFile.Csv<TextRow>(path).WithBomDetection(false).Single().Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OptionsValidateImmediatelyWithoutOpeningAFile()
    {
        var source = TabularFile.Csv<TextRow>("does-not-exist.csv");
        foreach (var c in new[] { '\0', '\r', '\n', '\ud800', '\udfff' })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => source.WithDelimiter(c));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.WithQuote(c));
        }
        Assert.Throws<ArgumentException>(() => source.WithDelimiter('"'));
        Assert.Throws<ArgumentException>(() => source.WithQuote(','));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithBufferSize(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithBufferSize(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxFieldLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithMaxRecordLength(-1));
    }
}

[TabularRecord]
public sealed partial class LiteralHeaderRow
{
    [Column(" Text ")] public required string Value { get; init; }
}

[TabularRecord]
public sealed partial class NullableNumberRow
{
    public int? Value { get; init; }
}
