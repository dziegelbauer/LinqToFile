using System.Globalization;
using System.Text;
using LinqToFile.Infrastructure;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class TokenTests
{
    private static CsvSource<T> Read<T>(string value) where T : class, ITabularRecord<T> =>
        TabularFile.Csv<T>(() => new MemoryStream(Encoding.UTF8.GetBytes("Value\n\"" + value.Replace("\"", "\"\"") + "\"\n"))).WithBufferSize(1);

    [Theory]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("nO", false)]
    public async Task BooleanPairReplacesDefaultTextAndHonorsComparison(string text, bool expected)
    {
        var source = Read<TokenBoolean>(text);
        Assert.Equal(expected, source.Single().Value);
        Assert.Equal(expected, (await source.ToArrayAsync())[0].Value);
        Assert.Throws<TabularConversionException>(() => Read<TokenBoolean>("true").Single());
        Assert.Throws<TabularConversionException>(() => Read<TokenBoolean>("false").Single());
        Assert.Throws<TabularConversionException>(() => Read<TokenBoolean>(" yes ").Single());
        Assert.True(Read<TokenBoolean>(" yes ").WithFieldTrimming().Single().Value);
    }

    [Fact]
    public void CaseSensitiveBooleanAndNullComparisonStayIndependent()
    {
        Assert.True(Read<TokenSensitiveBoolean>("Yes").Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<TokenSensitiveBoolean>("YES").Single());
        Assert.Null(Read<TokenText>("NULL").Single().Value);
        Assert.Equal("null", Read<TokenText>("null").Single().Value);
        Assert.Null(Read<TokenInsensitiveText>("nUlL").Single().Value);
        Assert.Equal(" NULL ", Read<TokenText>(" NULL ").Single().Value);
        Assert.Null(Read<TokenText>(" NULL ").WithFieldTrimming().Single().Value);
        Assert.True(Read<TokenFileBoolean>("file").WithCulture(CultureInfo.GetCultureInfo("tr-TR")).Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<TokenFileBoolean>("fıle").WithCulture(CultureInfo.GetCultureInfo("tr-TR")).Single());
    }

    [Fact]
    public void EmptyFieldsRetainExistingRulesAndNullPrecedesOtherConversions()
    {
        Assert.Equal("", Read<TokenText>("").Single().Value);
        Assert.Null(Read<TokenNumber>("").Single().Value);
        Assert.Null(Read<TokenNullableBoolean>("").Single().Value);
        Assert.Null(Read<TokenNullableBoolean>("NULL").Single().Value);
        Assert.False(Read<TokenNullableBoolean>("no").Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<TokenBoolean>("").Single());
        Assert.Null(Read<TokenNumber>("0").Single().Value);
        Assert.Equal(5, Read<TokenNumber>("5").Single().Value);
        Assert.Null(Read<TokenDate>("NULL").Single().Value);
        Assert.Equal(new DateOnly(2026, 12, 31), Read<TokenDate>("31/12/2026").Single().Value);
        Assert.Throws<TabularConversionException>(() => Read<TokenDate>("2026-12-31").Single());
        Assert.Null(Read<TokenEnum>("Ready").Single().Value);
        Assert.Equal(TokenState.Done, Read<TokenEnum>("Done").Single().Value);
    }

    [Fact]
    public void NativeXlsxValuesCannotBeConsumedByTextTokens()
    {
        using var boolean = Create(SingleColumn("1", "b"));
        Assert.True(TabularFile.Xlsx<TokenNullableBoolean>(boolean).Single().Value);
        using var numeric = Create(SingleColumn("1"));
        Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<TokenNumericBoolean>(numeric).Single());
        using var number = Create(SingleColumn("0"));
        Assert.Equal(0, TabularFile.Xlsx<TokenNumber>(number).Single().Value);
        using var numberText = Create(SingleColumn("0", "str"));
        Assert.Null(TabularFile.Xlsx<TokenNumber>(numberText).Single().Value);
        using var date = Create(SingleColumn("2026-12-31", "d"));
        Assert.Equal(new DateOnly(2026, 12, 31), TabularFile.Xlsx<TokenReservedDate>(date).Single().Value);
        using var nativeString = Create(SingleColumn("0"));
        Assert.Equal("0", TabularFile.Xlsx<TokenNumericText>(nativeString).Single().Value);
        using var cached = Create(Row(1, Text("A1", "Value")) + Row(2, Cell("A2", "s", "0")),
            change: p => SharedStrings(p, "<si><t>NULL</t></si>"));
        Assert.Null(TabularFile.Xlsx<TokenText>(cached).WithSharedStringMemoryBudget(0).Single().Value);
    }

    [Fact]
    public void MissingXlsxCellsKeepTheirEmptyValueMeaning()
    {
        using var number = Create(Row(1, Text("A1", "Value"), Text("B1", "Extra")) + Row(2, Text("B2", "anchor")));
        using var text = Create(Row(1, Text("A1", "Value"), Text("B1", "Extra")) + Row(2, Text("B2", "anchor")));
        Assert.Null(TabularFile.Xlsx<TokenNumber>(number).Single().Value);
        Assert.Equal("", TabularFile.Xlsx<TokenText>(text).Single().Value);
    }

    [Fact]
    public async Task CsvWritesTokensAndPreservesNullEmptyAndValueStates()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<TokenNullableBoolean>(path);
            await file.CreateAsync(new[] { new TokenNullableBoolean { Value = true }, new TokenNullableBoolean { Value = false }, new TokenNullableBoolean() });
            Assert.Equal("Value\nyes\nno\nNULL\n", await File.ReadAllTextAsync(path));
            await file.AppendRangeAsync(new[] { new TokenNullableBoolean { Value = true } });
            await file.InsertRangeAsync(1, new[] { new TokenNullableBoolean() });
            Assert.Equal(new bool?[] { true, null, false, null, true }, file.Select(r => r.Value));
            File.Delete(path);
            var text = TabularFile.Csv<TokenText>(path);
            text.Create(new[] { new TokenText(), new TokenText { Value = "" }, new TokenText { Value = "null" } });
            Assert.Equal("Value\nNULL\n\"\"\nnull\n", await File.ReadAllTextAsync(path));
            Assert.Equal([null, "", "null"], text.Select(r => r.Value));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false, "NULL")]
    [InlineData(true, " NULL ")]
    public void NonNullTextCollisionsThrowPrivatelyWithoutChangingStagedDestination(bool trim, string value)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<TokenText>(path).WithFieldTrimming(trim);
            file.Create(new[] { new TokenText { Value = "original" } });
            byte[] before = File.ReadAllBytes(path);
            var error = Assert.Throws<TabularWriteException>(() => file.Append(new TokenText { Value = value }));
            Assert.Equal(1, error.RecordNumber);
            Assert.Equal("Value", error.TargetMember);
            Assert.Null(error.InnerException);
            Assert.DoesNotContain(value, error.ToString(), StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NonNullNumericEnumAndTemporalValuesCannotSerializeAsNull()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            Assert.Throws<TabularWriteException>(() => TabularFile.Csv<TokenNumber>(path).Create(new[] { new TokenNumber { Value = 0 } }));
            Assert.False(File.Exists(path));
            Assert.Throws<TabularWriteException>(() => TabularFile.Csv<TokenEnum>(path).Create(new[] { new TokenEnum { Value = TokenState.Ready } }));
            Assert.False(File.Exists(path));
            Assert.Throws<TabularWriteException>(() => TabularFile.Csv<TokenReservedDate>(path).Create(new[] { new TokenReservedDate { Value = new(2026, 12, 31) } }));
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NullPriorityAndOutputCollisionUseTheirOwnComparison()
    {
        Assert.Null(Read<TokenOverlap>("YES").Single().Value);
        Assert.True(Read<TokenOverlap>("yes").Single().Value);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<TokenOverlap>(path);
            file.Create(new[] { new TokenOverlap { Value = true }, new TokenOverlap() });
            Assert.Equal("Value\nyes\nYES\n", File.ReadAllText(path));
            Assert.Equal(new bool?[] { true, null }, file.Select(r => r.Value));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TokensUseCsvDialectEscapingLimitsAndOwnedStrings()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            var file = TabularFile.Csv<TokenLiteralBoolean>(path).WithDelimiter(';').WithQuote('\'');
            file.Create(new[] { new TokenLiteralBoolean { Value = true }, new TokenLiteralBoolean() });
            Assert.Equal("Value\n'oui,\n\"雪\"'\nnon\n", File.ReadAllText(path));
            Assert.Equal(new[] { true, false }, file.Select(r => r.Value));
            File.Delete(path);
            Assert.Throws<TabularWriteException>(() => file.WithMaxFieldLength(6).Create(new[] { new TokenLiteralBoolean { Value = true } }));
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReadFailuresRetainWorksheetContextWithoutRawValues()
    {
        using var stream = Create(SingleColumn("private-invalid-token", "str"));
        var error = Assert.Throws<TabularConversionException>(() => TabularFile.Xlsx<TokenBoolean>(stream).Single());
        Assert.Equal("A2", error.CellReference);
        Assert.Equal("Data", error.WorksheetName);
        Assert.Equal("Value", error.TargetMember);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private-invalid-token", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TokenMatchingAndOutputGuardsAddNoPerFieldAllocations()
    {
        var row = new TabularRow(16, null);
        row.Append("YES"); row.EndField();
        var output = new TabularValue(CultureInfo.InvariantCulture, true);
        void Convert()
        {
            output.SetText(row.IsNullToken(0, "NULL", StringComparison.Ordinal) ? "NULL" :
                row.ReadBooleanTokens(0, "Value", "yes", "no", StringComparison.OrdinalIgnoreCase) ? "yes" : "no");
            output.RejectNullToken("NULL", StringComparison.Ordinal);
        }
        for (int i = 0; i < 20000; i++) Convert();
        long bytes = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) Convert();
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, bytes);
        Assert.Equal("yes", output.Text.ToString());
    }
}

public enum TokenState { Ready, Done }
[TabularRecord] public sealed partial class TokenBoolean
{ [BooleanTokens("yes", "no")] public bool Value { get; init; } }
[TabularRecord] public sealed partial class TokenSensitiveBoolean
{ [BooleanTokens("Yes", "No", Comparison = StringComparison.Ordinal)] public bool Value { get; init; } }
[TabularRecord] public sealed partial class TokenNumericBoolean
{ [BooleanTokens("1", "0")] public bool Value { get; init; } }
[TabularRecord] public sealed partial class TokenFileBoolean
{ [BooleanTokens("FILE", "NO")] public bool Value { get; init; } }
[TabularRecord] public sealed partial class TokenNullableBoolean
{ [BooleanTokens("yes", "no"), NullToken("NULL")] public bool? Value { get; init; } }
[TabularRecord] public sealed partial class TokenText
{ [NullToken("NULL")] public string? Value { get; init; } }
[TabularRecord] public sealed partial class TokenInsensitiveText
{ [NullToken("NULL", Comparison = StringComparison.OrdinalIgnoreCase)] public string? Value { get; init; } }
[TabularRecord] public sealed partial class TokenNumber
{ [NullToken("0")] public int? Value { get; init; } }
[TabularRecord] public sealed partial class TokenNumericText
{ [NullToken("0")] public string? Value { get; init; } }
[TabularRecord] public sealed partial class TokenDate
{ [NullToken("NULL"), TemporalFormat("dd/MM/yyyy")] public DateOnly? Value { get; init; } }
[TabularRecord] public sealed partial class TokenReservedDate
{ [NullToken("2026-12-31"), TemporalFormat("O")] public DateOnly? Value { get; init; } }
[TabularRecord] public sealed partial class TokenEnum
{ [NullToken("Ready")] public TokenState? Value { get; init; } }
[TabularRecord] public sealed partial class TokenOverlap
{ [NullToken("YES"), BooleanTokens("yes", "no")] public bool? Value { get; init; } }
[TabularRecord] public sealed partial class TokenLiteralBoolean
{ [BooleanTokens("oui,\n\"雪\"", "non")] public bool Value { get; init; } }
