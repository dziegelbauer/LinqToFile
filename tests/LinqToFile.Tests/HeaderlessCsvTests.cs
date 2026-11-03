using System.Globalization;
using System.Text;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class HeaderlessCsvTests
{
    private static CsvSource<HeaderlessRow> Source(string csv, bool indexes) =>
        Configure(TabularFile.Csv<HeaderlessRow>(() => new ProbeStream(Encoding.UTF8.GetBytes(csv), chunkSize: 1)), indexes);

    private static CsvSource<HeaderlessRow> Configure(CsvSource<HeaderlessRow> source, bool indexes) =>
        indexes ? source.WithColumnIndexes() : source.WithColumnNames("id", "Unused", "text");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstRecordIsDataAndGapsMultilineUnicodeAndTinyBuffersWork(bool indexes)
    {
        var source = Source("\n1,skip,\"雪\r\nAda\"\r\n2,skip,Grace", indexes).WithBufferSize(1);
        var rows = source.ToArray();
        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.Id));
        Assert.Equal("雪\r\nAda", rows[0].Text);
        Assert.Equal("Grace", rows[1].Text);
        var asyncRows = await source.ToArrayAsync();
        Assert.Equivalent(rows, asyncRows, strict: true);
        Assert.Equal("雪\r\nAda", rows[0].Text);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "\n\r\n\r")]
    [InlineData(true, "\n\r\n\r")]
    public async Task EmptyInputReturnsNoRecords(bool indexes, string csv)
    {
        var source = Source(csv, indexes);
        Assert.Empty(source);
        Assert.Empty(await source.ToArrayAsync());
    }

    [Fact]
    public async Task AsyncOnlyFactoryUsesOnlyAsyncIoAndDisposesItsStream()
    {
        var stream = new ProbeStream("1,x,Ada\n2,x,Grace"u8.ToArray(), 1, asyncOnly: true, yieldReads: true);
        var source = TabularFile.CsvAsync<HeaderlessRow>(_ => ValueTask.FromResult<Stream>(stream)).WithColumnIndexes();
        Assert.Equal(2, (await source.ToListAsync()).Count);
        Assert.Equal(0, stream.SyncReads);
        Assert.True(stream.AsyncReads > 0);
        Assert.Equal(1, stream.AsyncDisposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedStreamStartsAtCurrentPositionAndSharesSingleUseAcrossModes(bool indexes)
    {
        await using var stream = new ProbeStream("prefix1,x,Ada"u8.ToArray(), initialOffset: 6);
        var original = TabularFile.Csv<HeaderlessRow>(stream);
        Assert.Equal(1, Assert.Single(await Configure(original, indexes).ToArrayAsync()).Id);
        Assert.False(stream.IsDisposed);
        Assert.Throws<InvalidOperationException>(() => original.WithHeaders().ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedStreamIsDisposedOnEarlyExitFailureAndCancellation(bool indexes)
    {
        var stream = new ProbeStream("1,x,Ada\n2,x,Grace"u8.ToArray());
        using (var iterator = Configure(TabularFile.Csv<HeaderlessRow>(stream, leaveOpen: false), indexes).GetEnumerator())
            Assert.True(iterator.MoveNext());
        Assert.True(stream.IsDisposed);
        var failed = new ProbeStream("bad,x,Ada"u8.ToArray());
        Assert.Throws<TabularConversionException>(() => Configure(TabularFile.Csv<HeaderlessRow>(failed, false), indexes).ToArray());
        Assert.True(failed.IsDisposed);
        using var cts = new CancellationTokenSource();
        var canceled = new ProbeStream("1,x,Ada\n2,x,Grace"u8.ToArray());
        await using var enumerator = Configure(TabularFile.Csv<HeaderlessRow>(canceled, false), indexes).GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.True(canceled.IsDisposed);
    }

    [Theory]
    [InlineData(false, "1,x")]
    [InlineData(false, "1,x,Ada,extra")]
    [InlineData(false, "1,x,Ada\n2,x")]
    [InlineData(true, "1,x,Ada\n2,x")]
    [InlineData(true, "1,x,Ada\n2,x,Grace,extra")]
    public async Task WidthChecksApplyToEveryDataRecord(bool indexes, string csv)
    {
        Assert.Throws<CsvFormatException>(() => Source(csv, indexes).ToArray());
        await Assert.ThrowsAsync<CsvFormatException>(async () => await Source(csv, indexes).ToArrayAsync());
    }

    [Fact]
    public async Task IndexModeAcceptsUnmappedTrailingFieldsAndChecksFirstRecordBounds()
    {
        Assert.Equal(2, Source("1,x,Ada,extra\n2,x,Grace,more", true).Count());
        var error = Assert.Throws<TabularSchemaException>(() => Source("1,x", true).ToArray());
        Assert.Equal(1, error.RecordNumber);
        Assert.Equal(3, error.ColumnIndex);
        Assert.Equal("Text", error.TargetMember);
        Assert.Null(error.ColumnHeader);
        await Assert.ThrowsAsync<TabularSchemaException>(async () => await Source("1,x", true).ToArrayAsync());
        var huge = TabularFile.Csv<HeaderlessHugeIndex>(() => new MemoryStream("1"u8.ToArray())).WithColumnIndexes();
        var hugeError = Assert.Throws<TabularSchemaException>(() => huge.ToArray());
        Assert.Null(hugeError.ColumnIndex); // The existing one-based int diagnostic cannot represent int.MaxValue + 1.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SkippedFieldsStillReceiveSyntaxAndLengthValidation(bool indexes)
    {
        Assert.Throws<CsvFormatException>(() => Source("1,bad\"quote,Ada", indexes).ToArray());
        Assert.Throws<CsvFormatException>(() => Source("1,long,Ada", indexes).WithMaxFieldLength(3).ToArray());
        await Assert.ThrowsAsync<CsvFormatException>(async () => await Source("1,long,Ada", indexes).WithMaxFieldLength(3).ToArrayAsync());
        Assert.Throws<CsvFormatException>(() => Source("1,x,Ada", indexes).WithMaxRecordLength(6).ToArray());
        Assert.Throws<CsvFormatException>(() => Source("\n", indexes).WithBlankLineSkipping(false).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsCountDataRecordsAndPhysicalLinesWithoutDisclosingValues(bool indexes)
    {
        var error = Assert.Throws<TabularConversionException>(() => Source("\n1,x,\"Ada\nLovelace\"\nSECRET,x,Grace", indexes).ToArray());
        Assert.Equal(2, error.RecordNumber);
        Assert.Equal(4, error.LineNumber);
        Assert.Equal(1, error.ColumnIndex);
        Assert.Equal("Id", error.TargetMember);
        Assert.DoesNotContain("SECRET", error.ToString());
        if (!indexes) Assert.Equal("id", error.ColumnHeader);
        else Assert.Null(error.ColumnHeader);
    }

    [Fact]
    public void SuppliedNamesAreCopiedValidatedAndRecheckedWhenHeaderOptionsChange()
    {
        var original = TabularFile.Csv<HeaderlessRow>("missing.csv");
        Assert.Throws<ArgumentNullException>(() => original.WithColumnNames(null!));
        Assert.Throws<ArgumentException>(() => original.WithColumnNames());
        Assert.Throws<ArgumentException>(() => original.WithColumnNames("id", " ", "text"));
        Assert.Throws<ArgumentException>(() => original.WithColumnNames("id", null!, "text"));
        Assert.Throws<ArgumentException>(() => original.WithColumnNames("id", "id", "text"));
        Assert.Throws<ArgumentException>(() => original.WithColumnNames("id", "Other"));
        var duplicate = original.WithColumnNames("id", "ID", "text");
        Assert.Throws<ArgumentException>(() => duplicate.WithHeaderComparison(StringComparer.OrdinalIgnoreCase));
        var padded = original.WithColumnNames("id", " id ", "text");
        Assert.Throws<ArgumentException>(() => padded.WithHeaderTrimming());
        string[] names = ["id", "unused", "text"];
        var source = TabularFile.Csv<HeaderlessRow>(() => new MemoryStream("1,x,Ada"u8.ToArray())).WithColumnNames(names);
        names[0] = "bad";
        Assert.Equal(1, source.Single().Id);
        var trimmed = TabularFile.Csv<HeaderlessRow>(() => new MemoryStream("1,x,Ada"u8.ToArray()))
            .WithHeaderComparison(StringComparer.OrdinalIgnoreCase).WithHeaderTrimming().WithColumnNames(" ID ", " unused ", " TEXT ");
        Assert.Equal("Ada", trimmed.Single().Text);
    }

    [Fact]
    public void MissingIndexesFailImmediatelyWithoutOpeningInput()
    {
        Assert.Throws<ArgumentException>(() => TabularFile.Csv<AnnotatedIdRow>("missing.csv").WithColumnIndexes());
        Assert.Throws<ArgumentException>(() => TabularFile.Csv<HeaderlessPartialIndex>("missing.csv").WithColumnIndexes());
    }

    [Fact]
    public void ModesReplaceEachOtherWithoutChangingEarlierConfigurations()
    {
        var data = TabularFile.Csv<HeaderlessRow>(() => new MemoryStream("1,x,Ada"u8.ToArray()));
        var indexed = data.WithColumnIndexes();
        var named = indexed.WithColumnNames("text", "unused", "id");
        Assert.Equal(1, indexed.Single().Id);
        Assert.Throws<TabularConversionException>(() => named.Single());
        Assert.Equal(1, named.WithColumnIndexes().Single().Id);
        var headed = TabularFile.Csv<HeaderlessRow>(() => new MemoryStream("text,id\nAda,1"u8.ToArray()));
        Assert.Equal(1, headed.Single().Id); // Index annotations alone have no effect.
        Assert.Equal(1, headed.WithColumnIndexes().WithHeaders().Single().Id);
        Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<HeaderlessRow>(() => new MemoryStream()).ToArray());
    }

    [Fact]
    public async Task HeaderlessConfigurationDoesNotChangePathWriteOrXlsxMapping()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            CsvFile<HeaderlessRow> file = TabularFile.Csv<HeaderlessRow>(path).WithColumnNames("text", "id").WithColumnIndexes();
            Assert.Equal(1, file.Create([new HeaderlessRow { Id = 1, Text = "Ada" }]));
            Assert.StartsWith("id,text\n", File.ReadAllText(path));
            Assert.Equal(1, await file.AppendAsync(new HeaderlessRow { Id = 2, Text = "Grace" }));
            Assert.Equal(1, file.Insert(0, new HeaderlessRow { Id = 3, Text = "Lin" }));
            Assert.Equal(new[] { 3, 1, 2 }, file.WithHeaders().Select(r => r.Id));
        }
        finally { File.Delete(path); }
        using var workbook = new MemoryStream();
        TabularFile.XlsxWriter<HeaderlessRow>(workbook).Write(new HeaderlessRow { Id = 4, Text = "Ada" });
        workbook.Position = 0;
        Assert.Equal(4, Assert.Single(await TabularFile.Xlsx<HeaderlessRow>(workbook).ToArrayAsync()).Id);
    }

    [Fact]
    public async Task CustomDialectCultureTrimmingAndAnnotationsApplyToFirstRecord()
    {
        var source = TabularFile.Csv<HeaderlessAnnotated>(() => new MemoryStream("'12,50'; YES ;NULL;2026-11-03"u8.ToArray()))
            .WithColumnIndexes().WithDelimiter(';').WithQuote('\'').WithFieldTrimming()
            .WithCulture(CultureInfo.GetCultureInfo("fr-FR")).WithBufferSize(1);
        var row = Assert.Single(await source.ToArrayAsync());
        Assert.Equal(12.50m, row.Amount);
        Assert.True(row.Enabled);
        Assert.Null(row.Optional);
        Assert.Equal(new DateOnly(2026, 11, 3), row.Date);
    }
}

[TabularRecord]
public sealed partial class HeaderlessRow
{
    [Column("text"), ColumnIndex(2)] public string Text { get; init; } = "";
    [Column("id"), ColumnIndex(0)] public int Id { get; init; }
    [Ignore] public string Ignored { get; init; } = "";
}

[TabularRecord]
public sealed partial class HeaderlessPartialIndex
{
    [ColumnIndex(0)] public int Id { get; init; }
    public int Other { get; init; }
}

[TabularRecord]
public sealed partial class HeaderlessHugeIndex
{
    [ColumnIndex(int.MaxValue)] public int Value { get; init; }
}

[TabularRecord]
public sealed partial class HeaderlessAnnotated
{
    [ColumnIndex(0)] public decimal Amount { get; init; }
    [ColumnIndex(1), BooleanTokens("yes", "no")] public bool Enabled { get; init; }
    [ColumnIndex(2), NullToken("NULL")] public int? Optional { get; init; }
    [ColumnIndex(3), TemporalFormat("yyyy-MM-dd")] public DateOnly Date { get; init; }
}
