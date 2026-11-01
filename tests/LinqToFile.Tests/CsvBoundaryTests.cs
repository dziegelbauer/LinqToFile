using System.Text;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvBoundaryTests
{
    public static IEnumerable<object[]> BoundaryCases()
    {
        foreach (var encoding in new[] { "utf-8", "utf-16", "utf-16BE", "utf-32", "utf-32BE" })
        foreach (bool bom in new[] { false, true })
        foreach (int buffer in new[] { 1, 2, 3, 7 })
        foreach (int chunk in new[] { 1, 2, 5 })
            yield return [encoding, bom, buffer, chunk];
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void ReadsEveryEncodingAcrossTinyNonSeekableReads(string encodingName, bool bom, int bufferSize, int chunk)
    {
        var encoding = Encoding.GetEncoding(encodingName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        const string csv = "\r\nA,B\r\n\"雪😀,x\",\"a\"\"b\"\r\n\"\r\nz\",\r\n\"\",\"\"\r\nend,last";
        byte[] bytes = [.. bom ? encoding.GetPreamble() : [], .. encoding.GetBytes(csv)];
        using var stream = new ChunkedReadStream(bytes, chunk);
        var options = new CsvOptions
        {
            Encoding = bom ? new UTF8Encoding(false, true) : encoding,
            BufferSize = bufferSize
        };
        var rows = ReadAll(stream, options);
        Assert.Equal(5, rows.Count);
        Assert.Equal(new[] { "A", "B" }, rows[0]);
        Assert.Equal(new[] { "雪😀,x", "a\"b" }, rows[1]);
        Assert.Equal(new[] { "\r\nz", "" }, rows[2]);
        Assert.Equal(new[] { "", "" }, rows[3]);
        Assert.Equal(new[] { "end", "last" }, rows[4]);
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public void DisabledDetectionStillRemovesMatchingBomForNoPreambleEncoding(string name)
    {
        var emitting = Encoding.GetEncoding(name);
        Encoding configured = name switch
        {
            "utf-8" => new UTF8Encoding(false, true),
            "utf-16" => new UnicodeEncoding(false, false, true),
            "utf-16BE" => new UnicodeEncoding(true, false, true),
            "utf-32" => new UTF32Encoding(false, false, true),
            _ => new UTF32Encoding(true, false, true)
        };
        using var stream = new ChunkedReadStream([.. emitting.GetPreamble(), .. emitting.GetBytes("A\n雪")], 1);
        var rows = ReadAll(stream, new CsvOptions { Encoding = configured, DetectBom = false, BufferSize = 1 });
        Assert.Equal("A", rows[0][0]);
        Assert.Equal("雪", rows[1][0]);
    }

    [Fact]
    public void DisabledDetectionNeverSwitchesToBomEncoding()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. "A\nx"u8];
        using var disabled = new ChunkedReadStream(bytes, 1);
        var literal = ReadAll(disabled, new CsvOptions { Encoding = Encoding.Latin1, DetectBom = false });
        Assert.Equal("ï»¿A", literal[0][0]);
        using var enabled = new ChunkedReadStream(bytes, 1);
        Assert.Equal("A", ReadAll(enabled, new CsvOptions { Encoding = Encoding.Latin1 })[0][0]);
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public void InvalidUnicodeAfterBomThrowsEvenAcrossOneByteReads(string name)
    {
        var encoding = Encoding.GetEncoding(name);
        byte[] invalid = name switch
        {
            "utf-8" => [0xF0, 0x9F],
            "utf-16" => [0x00, 0xD8],
            "utf-16BE" => [0xD8, 0x00],
            "utf-32" => [0x00, 0x00, 0x11, 0x00],
            _ => [0x00, 0x11, 0x00, 0x00]
        };
        using var stream = new ChunkedReadStream([.. encoding.GetPreamble(), .. encoding.GetBytes("A\n"), .. invalid], 1);
        Assert.Throws<DecoderFallbackException>(() => ReadAll(stream, new CsvOptions { BufferSize = 1 }));
    }

    [Fact]
    public void DetectionPreservesExplicitDecoderReplacementPolicy()
    {
        var encoding = (Encoding)new UTF8Encoding(false).Clone();
        encoding.DecoderFallback = new DecoderReplacementFallback("<bad>");
        using var stream = new ChunkedReadStream([0xEF, 0xBB, 0xBF, .. "A\n"u8, 0xFF], 1);
        Assert.Equal("<bad>", ReadAll(stream, new CsvOptions { Encoding = encoding, BufferSize = 1 })[1][0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]
    public void ShortPrefixesDoNotLoseBytes(string text)
    {
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(text), 1);
        var rows = ReadAll(stream, new CsvOptions { BufferSize = 1 });
        if (text.Length == 0) Assert.Empty(rows);
        else Assert.Equal(text, Assert.Single(rows)[0]);
    }

    [Fact]
    public void ConsumesOnlyOneMatchingBom()
    {
        using var stream = new ChunkedReadStream([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. "A"u8], 1);
        Assert.Equal("\uFEFFA", Assert.Single(ReadAll(stream, new CsvOptions()))[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InternalReaderHonorsStreamOwnership(bool leaveOpen)
    {
        var stream = new ChunkedReadStream("A\nx"u8.ToArray(), 1);
        using (var reader = CsvTextReader.Open(stream, new CsvOptions(), leaveOpen))
            Assert.Equal("A\nx", reader.ReadToEnd());
        Assert.Equal(!leaveOpen, stream.IsDisposed);
        stream.Dispose();
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void RecordEndingsAndBlankLinesWorkAcrossOneCharacterBuffers(string ending)
    {
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(ending + "A,B" + ending + ending + "x," + ending), 1);
        var rows = ReadAll(stream, new CsvOptions { BufferSize = 1 });
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "x", "" }, rows[1]);
    }

    [Theory]
    [InlineData("a\"b")]
    [InlineData("\"a\"x")]
    [InlineData("\"a\" ")]
    [InlineData("\"unterminated")]
    [InlineData("\"a\"\"")]
    public void MalformedQuotingFailsAcrossTinyReadBoundaries(string csv)
    {
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(csv), 1);
        var error = Assert.Throws<CsvFormatException>(() => ReadAll(stream, new CsvOptions { BufferSize = 1 }));
        Assert.Equal(1, error.RecordNumber);
        Assert.Equal(1, error.ColumnIndex);
    }

    [Fact]
    public void LargeFieldGrowsWithoutInvalidatingPreviouslyOwnedValues()
    {
        var large = new string('x', 100_000);
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes("first\n\"" + large + "\"\nlast"), 3);
        var rows = ReadAll(stream, new CsvOptions { BufferSize = 3 });
        Assert.Equal("first", rows[0][0]);
        Assert.Equal(large, rows[1][0]);
        Assert.Equal("last", rows[2][0]);
    }

    [Theory]
    [InlineData("😀", 2, 4)]
    [InlineData("a\"\"b", 3, 6)]
    [InlineData("a\r\nb", 4, 6)]
    public void LimitsCountUtf16AndRawQuoteSyntaxExactly(string escaped, int fieldLength, int recordLength)
    {
        var csv = "\"" + escaped + "\"";
        using var stream = new ChunkedReadStream(Encoding.UTF8.GetBytes(csv), 1);
        Assert.Single(ReadAll(stream, new CsvOptions { BufferSize = 1, MaxFieldLength = fieldLength, MaxRecordLength = recordLength }));
        using var tooShort = new ChunkedReadStream(Encoding.UTF8.GetBytes(csv), 1);
        Assert.Throws<CsvFormatException>(() => ReadAll(tooShort,
            new CsvOptions { BufferSize = 1, MaxFieldLength = fieldLength - 1 }));
    }

    [Fact]
    public void OrdinaryStreamingDoesNotAllocatePerRecordParserStorage()
    {
        using var text = new RepeatingRows(100_064);
        var parser = new CsvParser(text, new CsvOptions { BufferSize = 64 });
        for (int i = 0; i < 64; i++) Assert.True(parser.Read());
        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = 0;
        long characters = 0;
        while (parser.Read())
        {
            count++;
            characters += parser.Record.GetField(0).Length + parser.Record.GetField(1).Length;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(100_000, count);
        Assert.Equal(200_000, characters);
        Assert.InRange(allocated, 0, 512);
    }

    private static List<string[]> ReadAll(Stream stream, CsvOptions options)
    {
        using var reader = CsvTextReader.Open(stream, options, leaveOpen: true);
        var parser = new CsvParser(reader, options);
        var rows = new List<string[]>();
        while (parser.Read())
        {
            var fields = new string[parser.Record.Count];
            for (int i = 0; i < fields.Length; i++) fields[i] = parser.Record.GetField(i).ToString();
            rows.Add(fields);
        }
        return rows;
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunk) : Stream
    {
        private int offset;
        internal bool IsDisposed { get; private set; }
        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Length is not available.");
        public override long Position { get => throw new NotSupportedException("Position is not available."); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int start, int count) => Read(buffer.AsSpan(start, count));
        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            int count = Math.Min(Math.Min(buffer.Length, chunk), bytes.Length - offset);
            bytes.AsSpan(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Seeking is not available.");
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { IsDisposed = true; base.Dispose(disposing); }
    }

    private sealed class RepeatingRows(int records) : TextReader
    {
        private int remaining = records;
        private int position;
        public override int Read(Span<char> buffer)
        {
            int written = 0;
            while (written < buffer.Length && remaining > 0)
            {
                ReadOnlySpan<char> row = "x,y\n";
                int count = Math.Min(buffer.Length - written, row.Length - position);
                row.Slice(position, count).CopyTo(buffer[written..]);
                position += count;
                written += count;
                if (position == row.Length) { position = 0; remaining--; }
            }
            return written;
        }
    }
}
