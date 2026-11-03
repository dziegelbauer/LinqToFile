using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace LinqToFile.Reading;

// Stateless encoded units are decoded with their original byte starts. Re-encoding decoded
// text would lose offsets when a replacement fallback consumes malformed bytes.
internal sealed class PositionedTextReader : TextReader
{
    private readonly Stream stream;
    private readonly byte[] bytes;
    private readonly char[] unit;
    private readonly int codePage;
    private int position, count, unitPosition, unitCount;
    private long bufferStart, unitStart;
    private bool ended;
    internal Encoding Encoding { get; }
    internal long[] ByteOffsets { get; private set; }

    private PositionedTextReader(Stream stream, byte[] bytes, int count, TextOptions options)
    {
        this.stream = stream; this.bytes = bytes; this.count = count;
        Encoding = EncodedTextReader.SelectEncoding(bytes.AsSpan(0, count), options, out int skip);
        codePage = Encoding.CodePage;
        if (!Encoding.IsSingleByte && Encoding.CodePage is not (65001 or 1200 or 1201 or 12000 or 12001))
            throw new NotSupportedException("CSV insertion supports UTF-8, UTF-16, UTF-32, and stateless single-byte encodings.");
        byte[] preamble = Encoding.GetPreamble();
        position = skip != 0 ? skip : preamble.Length != 0 && bytes.AsSpan(0, count).StartsWith(preamble) ? preamble.Length : 0;
        unit = new char[Math.Max(2, Encoding.GetMaxCharCount(4))];
        ByteOffsets = new long[options.BufferSize];
    }

    internal static PositionedTextReader Open(Stream stream, TextOptions options)
    {
        int prefix = Math.Max(4, options.Encoding.GetPreamble().Length);
        var bytes = new byte[Math.Max(4096, prefix)];
        int count = stream.ReadAtLeast(bytes, prefix, throwOnEndOfStream: false);
        return new(stream, bytes, count, options);
    }
    internal static async ValueTask<PositionedTextReader> OpenAsync(Stream stream, TextOptions options, CancellationToken token)
    {
        int prefix = Math.Max(4, options.Encoding.GetPreamble().Length);
        var bytes = new byte[Math.Max(4096, prefix)];
        int count = await stream.ReadAtLeastAsync(bytes, prefix, throwOnEndOfStream: false, token).ConfigureAwait(false);
        return new(stream, bytes, count, options);
    }

    private int UnitLength(ReadOnlySpan<byte> available)
    {
        switch (codePage)
        {
            case 1200:
            case 1201:
                if (available.Length < 2) return available.Length;
                bool bigEndian = codePage == 1201;
                ushort first = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(available) : BinaryPrimitives.ReadUInt16LittleEndian(available);
                if (first is >= 0xD800 and <= 0xDBFF && available.Length >= 4)
                {
                    ushort next = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(available[2..]) : BinaryPrimitives.ReadUInt16LittleEndian(available[2..]);
                    if (next is >= 0xDC00 and <= 0xDFFF) return 4;
                }
                return 2;
            case 12000:
            case 12001: return Math.Min(4, available.Length);
            default: return 1;
        }
    }

    private void PrepareRefill()
    {
        bytes.AsSpan(position, count - position).CopyTo(bytes);
        bufferStart += position; count -= position; position = 0;
    }
    private bool ReadAvailable(Span<char> destination, ref int written, CancellationToken token)
    {
        while (written < destination.Length)
        {
            if ((written & 1023) == 0) token.ThrowIfCancellationRequested();
            if (unitPosition < unitCount)
            {
                // A boundary inside a multi-character fallback has no separate byte address.
                ByteOffsets[written] = unitPosition == 0 ? unitStart : -1;
                destination[written++] = unit[unitPosition++];
                continue;
            }
            // Most CSV syntax and numeric/text content is ASCII. Avoid a Rune decode and
            // a virtual GetChars call per byte while retaining exact original offsets.
            if (codePage == 65001 && position < count && bytes[position] < 0x80)
            {
                do
                {
                    if ((written & 1023) == 0) token.ThrowIfCancellationRequested();
                    ByteOffsets[written] = bufferStart + position;
                    destination[written++] = (char)bytes[position++];
                } while (written < destination.Length && position < count && bytes[position] < 0x80);
                continue;
            }
            if (count - position < 4 && !ended) return false;
            if (position == count) return true;
            var available = bytes.AsSpan(position, count - position);
            int length;
            unitStart = bufferStart + position;
            if (codePage == 65001)
            {
                var status = Rune.DecodeFromUtf8(available, out var rune, out int consumed);
                length = Math.Max(1, consumed);
                unitCount = status == OperationStatus.Done ? rune.EncodeToUtf16(unit) : Encoding.GetChars(available[..length], unit);
            }
            else
            {
                length = UnitLength(available);
                unitCount = Encoding.GetChars(available[..length], unit);
            }
            unitPosition = 0;
            position += length;
        }
        return true;
    }

    private void EnsureOffsets(int size)
    {
        if (ByteOffsets.Length < size) ByteOffsets = new long[size];
    }
    public override int Read(Span<char> buffer)
    {
        EnsureOffsets(buffer.Length);
        int written = 0;
        while (!ReadAvailable(buffer, ref written, default))
        {
            PrepareRefill();
            int read = stream.Read(bytes.AsSpan(count));
            count += read; ended = read == 0;
        }
        return written;
    }
    public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));
    public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
    {
        EnsureOffsets(buffer.Length);
        int written = 0;
        while (!ReadAvailable(buffer.Span, ref written, cancellationToken))
        {
            PrepareRefill();
            int read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken).ConfigureAwait(false);
            count += read; ended = read == 0;
        }
        return written;
    }
}
