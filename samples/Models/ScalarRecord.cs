using LinqToFile;

namespace SampleModels;

public enum SampleStatus { Ready = 1, Done = 2 }

[Flags]
public enum SamplePermission { None = 0, Read = 1, Write = 2 }

public class ScalarRecordBase
{
    [Column("text")]
    public required string Name { get; init; }
}

[TabularRecord]
public sealed partial class ScalarRecord : ScalarRecordBase
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
    public SampleStatus State { get; init; }
    public SamplePermission Access { get; init; }
}

[TabularRecord]
public sealed partial class LocalizedPrice
{
    public decimal Value { get; init; }
}
