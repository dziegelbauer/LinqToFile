using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvCreationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Write-" + Guid.NewGuid().ToString("N"));
    public CsvCreationTests() => Directory.CreateDirectory(directory);
    private string PathFor(string name = "data.csv") => Path.Combine(directory, name);
    public void Dispose() => Directory.Delete(directory, true);
    private void NoTemporaryFiles() => Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CreatesDeterministicHeadersAndIncrementalRows(bool inPlace, bool asynchronous)
    {
        var source = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        var input = new Once<WriteRow>([new() { Number = 7, Text = " a,\"b\"\r\n雪😀 " }, new() { Number = -2, Text = null }]);
        long count = asynchronous ? await source.CreateAsync(input) : source.Create(input);
        Assert.Equal(2, count);
        Assert.Equal("A,Z\n7,\" a,\"\"b\"\"\r\n雪😀 \"\n-2,\n", File.ReadAllText(PathFor()));
        Assert.False(File.ReadAllBytes(PathFor()).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal([7, -2], source.Select(x => x.Number).ToArray());
        Assert.Equal(" a,\"b\"\r\n雪😀 ", source.First().Text);
        Assert.Equal("", source.Last().Text);
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeadersOnlyAndEmptyInputReturnZero(bool inPlace)
    {
        var a = TabularFile.Csv<WriteRow>(PathFor("a")).WithInPlaceWrites(inPlace);
        var b = TabularFile.Csv<WriteRow>(PathFor("b")).WithInPlaceWrites(inPlace);
        var c = TabularFile.Csv<WriteRow>(PathFor("c")).WithInPlaceWrites(inPlace);
        var d = TabularFile.Csv<WriteRow>(PathFor("d")).WithInPlaceWrites(inPlace);
        Assert.Equal(0, a.Create());
        Assert.Equal(0, await b.CreateAsync());
        Assert.Equal(0, c.Create(Array.Empty<WriteRow>()));
        Assert.Equal(0, await d.CreateAsync(Empty()));
        Assert.All(new[] { "a", "b", "c", "d" }, name => Assert.Equal("A,Z\n", File.ReadAllText(PathFor(name))));
        Assert.Empty(a);
        static async IAsyncEnumerable<WriteRow> Empty() { await Task.Yield(); yield break; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingFileIsRejectedWithoutEnumeratingInput(bool inPlace)
    {
        File.WriteAllText(PathFor(), "keep");
        var source = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        Assert.Throws<IOException>(() => source.Create(Fail()));
        await Assert.ThrowsAsync<IOException>(async () => await source.CreateAsync(Fail()));
        Assert.Equal("keep", File.ReadAllText(PathFor()));
        NoTemporaryFiles();
        static IEnumerable<WriteRow> Fail() => Enumerable.Range(0, 1).Select<int, WriteRow>(_ => throw new Exception("Must not enumerate"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InputFailureHonorsModeAndReleasesGuard(bool inPlace)
    {
        var source = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.CreateAsync(Fail()));
        Assert.Equal(inPlace, File.Exists(PathFor()));
        if (inPlace) File.Delete(PathFor());
        Assert.Equal(0, source.Create());
        NoTemporaryFiles();
        static IEnumerable<WriteRow> Fail()
        {
            yield return new() { Number = 1, Text = "ok" };
            throw new InvalidOperationException("input failed");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationCancellationStopsOnlyTemporaryModeAfterCreation(bool inPlace)
    {
        using var cts = new CancellationTokenSource();
        var source = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        CancellationToken observed = default;
        async IAsyncEnumerable<WriteRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            observed = token;
            yield return new() { Number = 1 };
            await Task.Yield();
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            yield return new() { Number = 2 };
        }
        if (inPlace)
        {
            Assert.Equal(2, await source.CreateAsync(Rows(), cts.Token));
            Assert.False(observed.CanBeCanceled);
            Assert.Equal(2, source.Count());
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.CreateAsync(Rows(), cts.Token));
            Assert.Equal(cts.Token, observed);
            Assert.False(File.Exists(PathFor()));
        }
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledCreationDoesNotTouchDiskOrInput(bool inPlace)
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var source = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.CreateAsync(cts.Token));
        Assert.False(File.Exists(PathFor())); NoTemporaryFiles();
    }

    [Fact]
    public void PublicationRaceDoesNotOverwriteTheOtherFile()
    {
        IEnumerable<WriteRow> Race()
        {
            File.WriteAllText(PathFor(), "other writer");
            yield return new() { Number = 1 };
        }
        Assert.Throws<IOException>(() => TabularFile.Csv<WriteRow>(PathFor()).Create(Race()));
        Assert.Equal("other writer", File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Fact]
    public async Task OverlappingNormalizedPathWritesFailWithoutWaiting()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<WriteRow> Rows()
        {
            entered.SetResult();
            await resume.Task;
            yield return new() { Number = 1 };
        }
        var pending = TabularFile.Csv<WriteRow>(PathFor()).CreateAsync(Rows()).AsTask();
        await entered.Task;
        try
        {
            var same = TabularFile.Csv<WriteRow>(Path.Combine(directory, ".", "data.csv")).WithDelimiter(';');
            Assert.Throws<InvalidOperationException>(() => same.Create());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await same.CreateAsync());
        }
        finally { resume.SetResult(); await pending; }
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SingleColumnEmptyValuesAreRecords(string? value)
    {
        var source = TabularFile.Csv<WriteText>(PathFor()).WithQuote('\'').WithFieldTrimming();
        Assert.Equal(1, source.Create([new() { Value = value }]));
        Assert.Equal("Value\n''\n", File.ReadAllText(PathFor()));
        Assert.Equal("", source.Single().Value);
    }

    public static IEnumerable<object[]> Encodings()
    {
        yield return [new UTF8Encoding(false, true)];
        yield return [new UTF8Encoding(true, true)];
        yield return [new UnicodeEncoding(false, true, true)];
        yield return [new UnicodeEncoding(true, true, true)];
        yield return [new UTF32Encoding(false, true, true)];
        yield return [new UTF32Encoding(true, true, true)];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public async Task DialectEncodingAndLargeFieldsRoundTrip(Encoding encoding)
    {
        string text = " raw;'quoted'\r\n雪😀" + new string('x', 8172) + "😀雪" + new string('x', 9000);
        var source = TabularFile.Csv<WriteText>(PathFor()).WithDelimiter(';').WithQuote('\'')
            .WithEncoding(encoding).WithBufferSize(1);
        await source.CreateAsync([new WriteText { Value = text }]);
        Assert.Equal(text, source.Single().Value);
        Assert.Equal(encoding.GetPreamble(), File.ReadAllBytes(PathFor()).Take(encoding.GetPreamble().Length));
        Assert.Equal(text, (await source.ToArrayAsync()).Single().Value);
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    [InlineData(LineEnding.Cr, "\r")]
    public void NewLineIsTypedAndPlatformIndependent(LineEnding ending, string text)
    {
        var source = TabularFile.Csv<WriteText>(PathFor()).WithNewLine(ending);
        source.Create([new() { Value = "abc" }]);
        Assert.Equal("Value" + text + "abc" + text, File.ReadAllText(PathFor()));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.WithNewLine((LineEnding)99));
    }

    [Theory]
    [InlineData("invariant")]
    [InlineData("fr-FR")]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    public async Task AllScalarsRoundTripWithCultureAndTickPrecision(string cultureName)
    {
        var culture = cultureName == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        var value = new AllScalarRow
        {
            UInt8 = byte.MaxValue, Int8 = sbyte.MinValue, Int16 = short.MinValue, UInt16 = ushort.MaxValue,
            Int32 = int.MinValue, UInt32 = uint.MaxValue, Int64 = long.MinValue, UInt64 = ulong.MaxValue,
            Int128 = Int128.MinValue, UInt128 = UInt128.MaxValue, Half = Half.Epsilon, Single = float.Epsilon,
            Double = double.MaxValue, Decimal = decimal.MaxValue, Character = '雪', Boolean = true,
            Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            Timestamp = new DateTime(2026, 11, 2, 13, 14, 15).AddTicks(1234567),
            TimestampOffset = new DateTimeOffset(2026, 11, 2, 13, 14, 15, TimeSpan.FromMinutes(-345)).AddTicks(1234567),
            Date = DateOnly.MinValue, Time = TimeOnly.MaxValue, Duration = TimeSpan.MinValue,
            State = SampleState.Ready, Access = SampleAccess.Read | SampleAccess.Write, Name = " raw ", OptionalText = ""
        };
        var source = TabularFile.Csv<AllScalarRow>(PathFor()).WithCulture(culture);
        await source.CreateAsync([value]);
        var result = source.Single();
        Assert.Equivalent(value, result, strict: true);
        Assert.Equal(DateTimeKind.Unspecified, result.Timestamp.Kind);
        Assert.Equal(value.TimestampOffset.Offset, result.TimestampOffset.Offset);
        var nullable = new NullableScalarRow
        {
            UInt8 = value.UInt8, Int8 = value.Int8, Int16 = value.Int16, UInt16 = value.UInt16,
            Int32 = value.Int32, UInt32 = value.UInt32, Int64 = value.Int64, UInt64 = value.UInt64,
            Int128 = value.Int128, UInt128 = value.UInt128, Half = value.Half, Single = value.Single,
            Double = value.Double, Decimal = value.Decimal, Character = value.Character, Boolean = value.Boolean,
            Id = value.Id, Timestamp = value.Timestamp, TimestampOffset = value.TimestampOffset,
            Date = value.Date, Time = value.Time, Duration = value.Duration, State = value.State, Access = value.Access,
            Name = value.Name, OptionalText = ""
        };
        var optionalSource = TabularFile.Csv<NullableScalarRow>(PathFor("nullable")).WithCulture(culture);
        optionalSource.Create([nullable, new() { Name = "", OptionalText = "" }]);
        Assert.Equivalent(nullable, optionalSource.First(), strict: true);
        Assert.Equivalent(new NullableScalarRow { Name = "", OptionalText = "" }, optionalSource.Last(), strict: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidValuesArePrivateAndRespectFailureMode(bool inPlace)
    {
        var source = TabularFile.Csv<WriteBadValue>(PathFor()).WithInPlaceWrites(inPlace);
        var error = Assert.Throws<TabularWriteException>(() => source.Create([new() { Number = double.NaN }]));
        Assert.Equal(1, error.RecordNumber); Assert.Equal(1, error.ColumnIndex);
        Assert.Equal("Number", error.ColumnHeader); Assert.Equal("Number", error.TargetMember);
        Assert.Null(error.InnerException); Assert.DoesNotContain("NaN", error.ToString());
        Assert.Equal(inPlace, File.Exists(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void RejectsNonFiniteDouble(double value) =>
        Assert.Throws<TabularWriteException>(() => TabularFile.Csv<WriteBadValue>(PathFor()).Create([new() { Number = value }]));

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void RejectsDateTimeKinds(DateTimeKind kind) =>
        Assert.Throws<TabularWriteException>(() => TabularFile.Csv<DateTimeOnlyRow>(PathFor())
            .Create([new() { Value = DateTime.SpecifyKind(new DateTime(2026, 11, 2), kind) }]));

    [Fact]
    public void GetterAndEncodingErrorsDoNotExposeRawData()
    {
        var error = Assert.Throws<TabularWriteException>(() => TabularFile.Csv<ThrowingWriteRow>(PathFor()).Create([new()]));
        Assert.DoesNotContain("secret-value", error.ToString()); Assert.Null(error.InnerException);
        var ascii = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        error = Assert.Throws<TabularWriteException>(() => TabularFile.Csv<WriteText>(PathFor()).WithEncoding(ascii)
            .Create([new() { Value = "雪" }]));
        Assert.DoesNotContain("雪", error.ToString()); Assert.False(File.Exists(PathFor()));
        NoTemporaryFiles();
    }

    [Fact]
    public void UnreadableModelsStillReadButCannotCreateEvenHeadersOnly()
    {
        File.WriteAllText(PathFor("read"), "Value\n42\n");
        Assert.Equal(42, TabularFile.Csv<SetterOnlyWriteRow>(PathFor("read")).Single().Observed);
        foreach (bool inPlace in new[] { false, true })
        {
            var error = Assert.Throws<TabularWriteException>(() => TabularFile.Csv<SetterOnlyWriteRow>(PathFor())
                .WithInPlaceWrites(inPlace).Create());
            Assert.Equal(0, error.RecordNumber); Assert.Equal("Value", error.TargetMember);
            Assert.False(File.Exists(PathFor()));
        }
        NoTemporaryFiles();
    }

    [Fact]
    public void LimitsCountUnescapedFieldsAndEscapedRecordCharacters()
    {
        var source = TabularFile.Csv<WriteText>(PathFor()).WithMaxFieldLength(5).WithMaxRecordLength(7);
        Assert.Equal(1, source.Create([new() { Value = "a\"b" }])); // "a""b" is six source units.
    }

    [Fact]
    public void OutputLimitsAndInvalidHeadersFailAtTheCorrectStage()
    {
        var field = TabularFile.Csv<WriteText>(PathFor("field")).WithMaxFieldLength(5);
        Assert.Throws<TabularWriteException>(() => field.Create([new() { Value = "123456" }]));
        var record = TabularFile.Csv<WriteText>(PathFor("record")).WithMaxRecordLength(5);
        Assert.Throws<TabularWriteException>(() => record.Create([new() { Value = "a\"b" }]));
        Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<WriteText>(PathFor()).WithMaxFieldLength(4).Create());
        Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<SpacedWriteHeader>(PathFor()).WithHeaderTrimming().Create());
        Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<EmptyWriteModel>(PathFor()).Create());
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public void NullRowsFailAndWhitespaceIsNeverTrimmedOnWrite()
    {
        Assert.Throws<ArgumentNullException>(() => TabularFile.Csv<WriteRow>(PathFor()).Create(null!));
        var error = Assert.Throws<TabularWriteException>(() => TabularFile.Csv<WriteRow>(PathFor())
            .Create([new() { Number = 1 }, null!]));
        Assert.Equal(2, error.RecordNumber); Assert.False(File.Exists(PathFor()));
        TabularFile.Csv<WriteText>(PathFor()).WithFieldTrimming().Create([new() { Value = "  keep  " }]);
        Assert.Equal("Value\n  keep  \n", File.ReadAllText(PathFor()));
    }

    [Fact]
    public void FlagsHandleAliasesOverlappingNamedMasksAndHighBitsWithoutReflection()
    {
        var source = TabularFile.Csv<OverlapWriteRow>(PathFor());
        source.Create([new() { Value = (OverlapWriteFlags)7 }]);
        Assert.Equal("Value\n\"AB, BC\"\n", File.ReadAllText(PathFor()));
        Assert.Equal((OverlapWriteFlags)7, source.Single().Value);
        TabularFile.Csv<OverlapWriteRow>(PathFor("alias")).Create([new() { Value = OverlapWriteFlags.Alias }]);
        Assert.Equal("Value\nAB\n", File.ReadAllText(PathFor("alias")));
        var wide = TabularFile.Csv<UnsignedAccessRow>(PathFor("wide"));
        wide.Create([new() { Value = UnsignedAccess.Top | UnsignedAccess.Bottom }]);
        Assert.Equal(UnsignedAccess.Top | UnsignedAccess.Bottom, wide.Single().Value);
        Assert.Throws<TabularWriteException>(() => TabularFile.Csv<OverlapWriteRow>(PathFor("bad"))
            .Create([new() { Value = (OverlapWriteFlags)8 }]));
        Assert.Throws<TabularWriteException>(() => TabularFile.Csv<OverlapWriteRow>(PathFor("zero"))
            .Create([new()]));
    }

    [Fact]
    public void NumericWriteAllocationDoesNotGrowWithRowCount()
    {
        long Measure(int count, string name)
        {
            var source = TabularFile.Csv<AllocationRow>(PathFor(name));
            var value = new AllocationRow { Value = 42 };
            IEnumerable<AllocationRow> Rows() { for (int i = 0; i < count; i++) yield return value; }
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(count, source.Create(Rows()));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(100_000, "warm");
        long small = long.MaxValue, large = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            small = Math.Min(small, Measure(1000, "small" + i));
            large = Math.Min(large, Measure(100_000, "large" + i));
        }
        Assert.InRange(large - small, -4096, 4096);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousInputUsesTheSameCancellationBoundary(bool inPlace)
    {
        using var cts = new CancellationTokenSource();
        IEnumerable<WriteRow> Rows()
        {
            yield return new() { Number = 1 };
            cts.Cancel();
            yield return new() { Number = 2 };
        }
        var file = TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
        if (inPlace) Assert.Equal(2, await file.CreateAsync(Rows(), cts.Token));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await file.CreateAsync(Rows(), cts.Token));
        Assert.Equal(inPlace, File.Exists(PathFor())); NoTemporaryFiles();
    }

    [Fact]
    public async Task IndependentInputCancellationStillFailsDirectWrites()
    {
        async IAsyncEnumerable<WriteRow> Rows()
        {
            yield return new() { Number = 1 };
            await Task.Yield();
            throw new OperationCanceledException("Input stopped independently.");
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await TabularFile.Csv<WriteRow>(PathFor())
            .WithInPlaceWrites().CreateAsync(Rows()));
        Assert.True(File.Exists(PathFor())); NoTemporaryFiles();
    }

    [Fact]
    public void InvariantTemporalRecognitionRetainsKindAndOffsetRequirements()
    {
        File.WriteAllText(PathFor("date"), "Value\n0001-01-01T00:00:00.0000000\n");
        var date = TabularFile.Csv<DateTimeOnlyRow>(PathFor("date")).WithCulture(CultureInfo.GetCultureInfo("ar-SA")).Single().Value;
        Assert.Equal(DateTime.MinValue, date); Assert.Equal(DateTimeKind.Unspecified, date.Kind);
        File.WriteAllText(PathFor("offset"), "Value\n2026-11-02T12:34:56.1234567\n");
        Assert.Throws<TabularConversionException>(() => TabularFile.Csv<OffsetOnlyRow>(PathFor("offset")).Single());
        File.WriteAllText(PathFor("date"), "Value\n2026-11-02T12:34:56.1234567+03:00\n");
        Assert.Throws<TabularConversionException>(() => TabularFile.Csv<DateTimeOnlyRow>(PathFor("date")).Single());
    }

    [Fact]
    public void ConfigurationSnapshotsRemainIndependentAndAsciiHasNoBom()
    {
        var encoding = (Encoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ":";
        var file = TabularFile.Csv<DecimalOnlyRow>(PathFor()).WithEncoding(encoding).WithCulture(culture);
        encoding.EncoderFallback = new EncoderReplacementFallback("changed");
        culture.NumberFormat.NumberDecimalSeparator = "!";
        file.WithNewLine(LineEnding.CrLf); // Do not mutate the original configuration.
        file.Create([new() { Value = 12.5m }]);
        Assert.Equal("Value\n12:5\n", File.ReadAllText(PathFor()));
        Assert.Equal(12.5m, file.Single().Value);
    }

    [Fact]
    public void MissingParentAndHeaderEncodingFailuresReleaseResources()
    {
        var file = TabularFile.Csv<WriteRow>(PathFor("missing/data.csv"));
        Assert.Throws<DirectoryNotFoundException>(() => file.Create());
        Directory.CreateDirectory(PathFor("missing"));
        Assert.Equal(0, file.Create());
        var ascii = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        Assert.Throws<TabularSchemaException>(() => TabularFile.Csv<UnicodeWriteHeader>(PathFor()).WithEncoding(ascii).Create());
        Assert.False(File.Exists(PathFor())); NoTemporaryFiles();
    }

    private sealed class Once<T>(IEnumerable<T> rows) : IEnumerable<T>
    {
        private bool used;
        public IEnumerator<T> GetEnumerator()
        {
            Assert.False(used); used = true; return rows.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public class WriteRowBase { [Column("Z")] public string? Text { get; set; } }
[TabularRecord]
public sealed partial class WriteRow : WriteRowBase
{
    [Column("A")] public int Number { get; init; }
    [Ignore] public string Ignored { get; set; } = "ignored";
}
[TabularRecord] public sealed partial class WriteText { public string? Value { get; init; } }
[TabularRecord] public sealed partial class WriteBadValue { public double Number { get; init; } }
[TabularRecord] public sealed partial class ThrowingWriteRow
{
    public string Value { get => throw new Exception("secret-value"); set { } }
}
[TabularRecord] public sealed partial class SetterOnlyWriteRow
{
    private int stored;
    public int Value { set => stored = value; }
    [Ignore] public int Observed => stored;
}
[TabularRecord] public sealed partial class SpacedWriteHeader { [Column(" Value ")] public int Value { get; init; } }
[TabularRecord] public sealed partial class EmptyWriteModel { }
[Flags] public enum OverlapWriteFlags { AB = 3, Alias = 3, BC = 6 }
[TabularRecord] public sealed partial class OverlapWriteRow { public OverlapWriteFlags Value { get; init; } }
[TabularRecord] public sealed partial class UnicodeWriteHeader { [Column("雪")] public int Value { get; init; } }
