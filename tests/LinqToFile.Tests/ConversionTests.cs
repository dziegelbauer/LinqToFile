using System.Globalization;
using Xunit;

namespace LinqToFile.Tests;

public sealed class ConversionTests
{
    private static object? allocationSink;
    private static readonly string[] Names = "UInt8|Int8|Int16|UInt16|Int32|UInt32|Int64|UInt64|Int128|UInt128|Half|Single|Double|Decimal|Character|Boolean|Id|Timestamp|TimestampOffset|Date|Time|Duration|State|Access|Name|OptionalText".Split('|');
    private static readonly string[] Values = """
        255|-128|-32768|65535|-2147483648|4294967295|-9223372036854775808|18446744073709551615|-170141183460469231731687303715884105728|340282366920938463463374607431768211455|1.5|-2.5e2|1.25e2|12.50|雪|tRuE|00000000-0000-0000-0000-000000000001|2026-11-01T01:30:45.1234567|2026-11-01T01:30:45.1234567-07:00|2026-11-01|01:30:45.1234567|1.02:03:04.0050000|Ready|Read, Write| raw |
        """.Split('|');

    private static string Csv(string[] values) =>
        string.Join(",", Names) + "\n" + string.Join(",", values.Select(v => "\"" + v.Replace("\"", "\"\"") + "\""));

    private static T Read<T>(string csv, Func<CsvSource<T>, CsvSource<T>>? configure = null) where T : class, ITabularRecord<T>
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, csv);
            var source = TabularFile.Csv<T>(path).WithBufferSize(1);
            return (configure?.Invoke(source) ?? source).Single();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MaterializesAllSupportedScalarTypes()
    {
        var row = Read<AllScalarRow>(Csv(Values));
        Assert.Equal(byte.MaxValue, row.UInt8);
        Assert.Equal(sbyte.MinValue, row.Int8);
        Assert.Equal(short.MinValue, row.Int16);
        Assert.Equal(ushort.MaxValue, row.UInt16);
        Assert.Equal(int.MinValue, row.Int32);
        Assert.Equal(uint.MaxValue, row.UInt32);
        Assert.Equal(long.MinValue, row.Int64);
        Assert.Equal(ulong.MaxValue, row.UInt64);
        Assert.Equal(Int128.MinValue, row.Int128);
        Assert.Equal(UInt128.MaxValue, row.UInt128);
        Assert.Equal((Half)1.5, row.Half);
        Assert.Equal(-250f, row.Single);
        Assert.Equal(125d, row.Double);
        Assert.Equal(12.50m, row.Decimal);
        Assert.Equal('雪', row.Character);
        Assert.True(row.Boolean);
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), row.Id);
        Assert.Equal(DateTimeKind.Unspecified, row.Timestamp.Kind);
        Assert.Equal(1, row.Timestamp.Hour);
        Assert.Equal(1_234_567, row.Timestamp.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(TimeSpan.FromHours(-7), row.TimestampOffset.Offset);
        Assert.Equal(1, row.TimestampOffset.Hour);
        Assert.Equal(new DateOnly(2026, 11, 1), row.Date);
        Assert.Equal(new TimeOnly(1, 30, 45).Add(TimeSpan.FromTicks(1_234_567)), row.Time);
        Assert.Equal(new TimeSpan(1, 2, 3, 4, 5), row.Duration);
        Assert.Equal(SampleState.Ready, row.State);
        Assert.Equal(SampleAccess.Read | SampleAccess.Write, row.Access);
        Assert.Equal(" raw ", row.Name);
        Assert.Equal("", row.OptionalText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyFieldsBecomeNullForEveryNullableValueType(bool quoted)
    {
        var csv = string.Join(",", Names) + "\n" + string.Join(",", Enumerable.Repeat(quoted ? "\"\"" : "", Names.Length));
        var row = Read<NullableScalarRow>(csv);
        Assert.Null(row.UInt8);
        Assert.Null(row.Int8);
        Assert.Null(row.Int16);
        Assert.Null(row.UInt16);
        Assert.Null(row.Int32);
        Assert.Null(row.UInt32);
        Assert.Null(row.Int64);
        Assert.Null(row.UInt64);
        Assert.Null(row.Int128);
        Assert.Null(row.UInt128);
        Assert.Null(row.Half);
        Assert.Null(row.Single);
        Assert.Null(row.Double);
        Assert.Null(row.Decimal);
        Assert.Null(row.Character);
        Assert.Null(row.Boolean);
        Assert.Null(row.Id);
        Assert.Null(row.Timestamp);
        Assert.Null(row.TimestampOffset);
        Assert.Null(row.Date);
        Assert.Null(row.Time);
        Assert.Null(row.Duration);
        Assert.Null(row.State);
        Assert.Null(row.Access);
        Assert.Equal("", row.Name);
        Assert.Equal("", row.OptionalText);
    }

    [Fact]
    public void NullableTypesAlsoMaterializeNonEmptyValues()
    {
        var row = Read<NullableScalarRow>(Csv(Values));
        Assert.Equal(Int128.MinValue, row.Int128);
        Assert.Equal(UInt128.MaxValue, row.UInt128);
        Assert.Equal((Half)1.5, row.Half);
        Assert.Equal(SampleAccess.Read | SampleAccess.Write, row.Access);
        Assert.Equal(new DateOnly(2026, 11, 1), row.Date);
        Assert.Equal(TimeSpan.FromHours(-7), row.TimestampOffset!.Value.Offset);
    }

    [Theory]
    [InlineData("UInt8", "256")]
    [InlineData("UInt8", "-1")]
    [InlineData("Int8", "-129")]
    [InlineData("Int16", "-32769")]
    [InlineData("UInt16", "65536")]
    [InlineData("Int32", "2147483648")]
    [InlineData("UInt32", "4294967296")]
    [InlineData("Int64", "9223372036854775808")]
    [InlineData("UInt64", "18446744073709551616")]
    [InlineData("Int128", "170141183460469231731687303715884105728")]
    [InlineData("UInt128", "340282366920938463463374607431768211456")]
    [InlineData("Half", "1e100")]
    [InlineData("Half", "NaN")]
    [InlineData("Single", "1e100")]
    [InlineData("Single", "-Infinity")]
    [InlineData("Double", "1e1000")]
    [InlineData("Double", "Infinity")]
    [InlineData("Double", "NaN")]
    [InlineData("Decimal", "79228162514264337593543950336")]
    [InlineData("Decimal", "1e2")]
    [InlineData("Int32", "1,000")]
    [InlineData("Decimal", "1,000.00")]
    [InlineData("Decimal", "$12.50")]
    [InlineData("Int32", "0x10")]
    [InlineData("Int32", "1\0")]
    [InlineData("Boolean", "true\0")]
    [InlineData("Boolean", "1")]
    [InlineData("Boolean", "yes")]
    [InlineData("Character", "AB")]
    [InlineData("Character", "😀")]
    [InlineData("Character", "")]
    [InlineData("Timestamp", "2026-11-01T01:30:00Z")]
    [InlineData("Timestamp", "2026-11-01T01:30:00+02:00")]
    [InlineData("TimestampOffset", "2026-11-01T01:30:00")]
    [InlineData("TimestampOffset", "2026-11-01")]
    [InlineData("TimestampOffset", "2026-11-01T01:30:00+15:00")]
    [InlineData("Date", "not-a-date")]
    [InlineData("Time", "25:00:00")]
    [InlineData("Duration", "not-a-duration")]
    [InlineData("Id", "not-a-guid")]
    [InlineData("State", "ready")]
    [InlineData("State", "1")]
    [InlineData("State", "Ready, Done")]
    [InlineData("Access", "Read, 2")]
    [InlineData("Access", "Read, Missing")]
    [InlineData("Access", "Read,")]
    [InlineData("Access", ",Read")]
    public void InvalidValuesHaveStructuredErrorsWithoutRawData(string member, string invalid)
    {
        var values = (string[])Values.Clone();
        int index = Array.IndexOf(Names, member);
        values[index] = invalid;
        var error = Assert.Throws<TabularConversionException>(() => Read<AllScalarRow>(Csv(values)));
        Assert.Equal(member, error.TargetMember);
        Assert.Equal(member, error.ColumnHeader);
        Assert.Equal(index + 1, error.ColumnIndex);
        Assert.Equal(2, error.RecordNumber);
        Assert.Equal(2, error.LineNumber);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("field value", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorTextDoesNotContainSecretValue()
    {
        var values = (string[])Values.Clone();
        values[0] = "private-secret-123";
        var error = Assert.Throws<TabularConversionException>(() => Read<AllScalarRow>(Csv(values)));
        Assert.DoesNotContain("private-secret-123", error.ToString());
    }

    [Fact]
    public void EveryNonNullableValueTypeRejectsEmptyFields()
    {
        for (int i = 0; i < Names.Length - 2; i++)
        {
            var values = (string[])Values.Clone();
            values[i] = "";
            var error = Assert.Throws<TabularConversionException>(() => Read<AllScalarRow>(Csv(values)));
            Assert.Equal(Names[i], error.TargetMember);
        }
    }

    [Fact]
    public void CultureIsConfigurableImmutableAndIndependentOfCurrentCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
        var path = Path.GetTempFileName();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            File.WriteAllText(path, "Price;Date;Timestamp;TimestampOffset;Time;Duration;Approx;Small\n12,50;31/12/2026;31/12/2026 13:14:15;31/12/2026 13:14:15 +02:30;13:14:15;02:03:04;1,25e2;1,5");
            var original = TabularFile.Csv<CultureRow>(path).WithDelimiter(';');
            var configured = original.WithCulture(culture);
            culture.NumberFormat.NumberDecimalSeparator = "!";
            var row = configured.Single();
            Assert.Equal(12.50m, row.Price);
            Assert.Equal(new DateOnly(2026, 12, 31), row.Date);
            Assert.Equal(DateTimeKind.Unspecified, row.Timestamp.Kind);
            Assert.Equal(13, row.Timestamp.Hour);
            Assert.Equal(TimeSpan.FromMinutes(150), row.TimestampOffset.Offset);
            Assert.Equal(new TimeOnly(13, 14, 15), row.Time);
            Assert.Equal(new TimeSpan(2, 3, 4), row.Duration);
            Assert.Equal(125d, row.Approx);
            Assert.Equal((Half)1.5, row.Small);
            Assert.Throws<TabularConversionException>(() => original.ToList());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(12.50m, Read<DecimalOnlyRow>("Value\n12.50").Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            File.Delete(path);
        }
        Assert.Throws<ArgumentNullException>(() => TabularFile.Csv<CultureRow>("unused").WithCulture(null!));
    }

    [Theory]
    [InlineData("0001-01-01T01:00:00+01:00", 60, 1)]
    [InlineData("9999-12-31T22:59:59-01:00", -60, 22)]
    [InlineData("2026-11-01T01:30:00Z", 0, 1)]
    [InlineData("2026-11-01T01:30:00+05:45", 345, 1)]
    public void DateTimeOffsetKeepsOffsetAndClockTimeIncludingRangeEdges(string input, int offsetMinutes, int hour)
    {
        var row = Read<OffsetOnlyRow>("Value\n" + input);
        Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), row.Value.Offset);
        Assert.Equal(hour, row.Value.Hour);
    }

    [Fact]
    public void OffsetFreeDateTimePreservesUnspecifiedClockTime()
    {
        var row = Read<DateTimeOnlyRow>("Value\n2026-11-01T01:30:00");
        Assert.Equal(DateTimeKind.Unspecified, row.Value.Kind);
        Assert.Equal(new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified), row.Value);
    }

    [Theory]
    [InlineData("Read, Write", 3)]
    [InlineData("Read, AliasRead", 1)]
    [InlineData("All", -1)]
    [InlineData("None", 0)]
    public void FlagsAcceptDeclaredNamesAndUnnamedCombinations(string text, int expected)
    {
        var row = Read<AccessOnlyRow>("Value\n\"" + text + "\"");
        Assert.Equal((SampleAccess)expected, row.Value);
    }

    [Fact]
    public void NumericAndEnumMappingAddsNoPerFieldStringAllocations()
    {
        using var text = new StringReader("42,340282366920938463463374607431768211455,1.25e2,\"Read, Write\"");
        var parser = new CsvParser(text, new CsvOptions());
        Assert.True(parser.Read());
        parser.Record.Headers = ["Value", "Wide", "Approx", "Flags"];
        int[] ordinals = [0, 1, 2, 3];
        var mapper = GetMapper<NumericEnumRow>();
        for (int i = 0; i < 20_000; i++)
        {
            Volatile.Write(ref allocationSink, mapper.Read(parser.Record, ordinals));
            Volatile.Write(ref allocationSink, new NumericEnumRow());
        }

        // One-time runtime work must not be mistaken for a per-field allocation.
        // A field string would allocate on every pass and still fail this unchanged bound.
        long mapped = long.MaxValue, objectsOnly = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10_000; i++) Volatile.Write(ref allocationSink, mapper.Read(parser.Record, ordinals));
            mapped = Math.Min(mapped, GC.GetAllocatedBytesForCurrentThread() - before);

            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10_000; i++) Volatile.Write(ref allocationSink, new NumericEnumRow());
            objectsOnly = Math.Min(objectsOnly, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.InRange(mapped - objectsOnly, 0, 512);
        Volatile.Write(ref allocationSink, null);
    }

    [Fact]
    public void NumericStylesAcceptLeadingSignsWhitespaceAndFloatingExponents()
    {
        var values = (string[])Values.Clone();
        values[Array.IndexOf(Names, "Int32")] = " +42 \t";
        values[Array.IndexOf(Names, "Double")] = " +1.25e+2 ";
        values[Array.IndexOf(Names, "Decimal")] = " +12.50 ";
        var row = Read<AllScalarRow>(Csv(values));
        Assert.Equal(42, row.Int32);
        Assert.Equal(125d, row.Double);
        Assert.Equal(12.50m, row.Decimal);
    }

    [Fact]
    public void UnsignedHighBitFlagsCombineWithoutSignedOverflow()
    {
        var row = Read<UnsignedAccessRow>("Value\n\"Top, Bottom\"");
        Assert.Equal((UnsignedAccess)((1UL << 63) | 1), row.Value);
    }

    private static LinqToFile.Infrastructure.RecordMapper<T> GetMapper<T>() where T : class, ITabularRecord<T> => T.Mapper;
}

public enum SampleState { Ready = 1, Done = 2 }

[Flags]
public enum SampleAccess { None = 0, Read = 1, Write = 2, AliasRead = Read, All = -1 }

[TabularRecord]
public sealed partial class AllScalarRow
{
    public byte UInt8 { get; init; }
    public sbyte Int8 { get; init; }
    public short Int16 { get; init; }
    public ushort UInt16 { get; init; }
    public int Int32 { get; init; }
    public uint UInt32 { get; init; }
    public long Int64 { get; init; }
    public ulong UInt64 { get; init; }
    public Int128 Int128 { get; init; }
    public UInt128 UInt128 { get; init; }
    public Half Half { get; init; }
    public float Single { get; init; }
    public double Double { get; init; }
    public decimal Decimal { get; init; }
    public char Character { get; init; }
    public bool Boolean { get; init; }
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTimeOffset TimestampOffset { get; init; }
    public DateOnly Date { get; init; }
    public TimeOnly Time { get; init; }
    public TimeSpan Duration { get; init; }
    public SampleState State { get; init; }
    public SampleAccess Access { get; init; }
    public required string Name { get; init; }
    public string? OptionalText { get; init; }
}

[TabularRecord]
public sealed partial class NullableScalarRow
{
    public byte? UInt8 { get; init; }
    public sbyte? Int8 { get; init; }
    public short? Int16 { get; init; }
    public ushort? UInt16 { get; init; }
    public int? Int32 { get; init; }
    public uint? UInt32 { get; init; }
    public long? Int64 { get; init; }
    public ulong? UInt64 { get; init; }
    public Int128? Int128 { get; init; }
    public UInt128? UInt128 { get; init; }
    public Half? Half { get; init; }
    public float? Single { get; init; }
    public double? Double { get; init; }
    public decimal? Decimal { get; init; }
    public char? Character { get; init; }
    public bool? Boolean { get; init; }
    public Guid? Id { get; init; }
    public DateTime? Timestamp { get; init; }
    public DateTimeOffset? TimestampOffset { get; init; }
    public DateOnly? Date { get; init; }
    public TimeOnly? Time { get; init; }
    public TimeSpan? Duration { get; init; }
    public SampleState? State { get; init; }
    public SampleAccess? Access { get; init; }
    public string? Name { get; init; }
    public string? OptionalText { get; init; }
}

[TabularRecord]
public sealed partial class CultureRow
{
    public decimal Price { get; init; }
    public DateOnly Date { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTimeOffset TimestampOffset { get; init; }
    public TimeOnly Time { get; init; }
    public TimeSpan Duration { get; init; }
    public double Approx { get; init; }
    public Half Small { get; init; }
}

[TabularRecord]
public sealed partial class DecimalOnlyRow { public decimal Value { get; init; } }
[TabularRecord]
public sealed partial class OffsetOnlyRow { public DateTimeOffset Value { get; init; } }
[TabularRecord]
public sealed partial class DateTimeOnlyRow { public DateTime Value { get; init; } }
[TabularRecord]
public sealed partial class AccessOnlyRow { public SampleAccess Value { get; init; } }

[TabularRecord]
public sealed partial class NumericEnumRow
{
    public int Value { get; init; }
    public UInt128 Wide { get; init; }
    public double Approx { get; init; }
    public SampleAccess Flags { get; init; }
}

[Flags]
public enum UnsignedAccess : ulong { Bottom = 1, Top = 1UL << 63 }
[TabularRecord]
public sealed partial class UnsignedAccessRow { public UnsignedAccess Value { get; init; } }
