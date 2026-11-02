using System.Text;
using LinqToFile.Reading;
using LinqToFile.Formats.Csv;
using LinqToFile.Writing;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class InsertionFoundationTests
{
    public static IEnumerable<object[]> Encodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (var encoding in new Encoding[]
        {
            new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true),
            new UTF32Encoding(false, true, true), new UTF32Encoding(true, true, true), Encoding.Latin1,
            Encoding.GetEncoding(37, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        }) yield return [encoding];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public async Task TracksActualRecordBytesAcrossBomMultilineBlankAndTinyBuffers(Encoding encoding)
    {
        string text = encoding.IsSingleByte ? "old" : "雪😀";
        string prefix = "Value\r\n\n\"" + text + "\r\ninside\"\n\r\n";
        byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(prefix + "last")).ToArray();
        foreach (int bufferSize in new[] { 1, 2, 3, 4096 })
        foreach (bool asynchronous in new[] { false, true })
        {
            using var stream = new ProbeStream(bytes, chunkSize: 1, asyncOnly: asynchronous, yieldReads: asynchronous);
            var options = new CsvOptions { Encoding = encoding, BufferSize = bufferSize };
            using var textReader = asynchronous ? await PositionedTextReader.OpenAsync(stream, options, default) : PositionedTextReader.Open(stream, options);
            using var reader = new CsvRecordReader(textReader, options, textReader.Encoding);
            Assert.True(asynchronous ? await reader.ReadAsync(default) : reader.Read());
            Assert.True(asynchronous ? await reader.ReadAsync(default) : reader.Read());
            Assert.Equal(encoding.GetPreamble().Length + encoding.GetByteCount("Value\r\n\n"), reader.RecordByteOffset);
            Assert.True(asynchronous ? await reader.ReadAsync(default) : reader.Read());
            Assert.Equal(encoding.GetPreamble().Length + encoding.GetByteCount(prefix), reader.RecordByteOffset);
            Assert.False(asynchronous ? await reader.ReadAsync(default) : reader.Read());
            if (asynchronous) Assert.Equal(0, stream.SyncReads);
        }
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    [InlineData(12000)]
    [InlineData(12001)]
    public void DecodingMatchesTheBclIncludingMalformedReplacementSequences(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, new DecoderReplacementFallback("?"));
        var random = new Random(42);
        foreach (int size in new[] { 1, 2, 3, 4, 17, 4095, 4096, 4097 })
        {
            var bytes = new byte[size]; random.NextBytes(bytes);
            using var stream = new MemoryStream(bytes);
            using var reader = PositionedTextReader.Open(stream, new CsvOptions { Encoding = encoding, DetectBom = false, BufferSize = 3 });
            var result = new StringBuilder();
            char[] buffer = new char[3];
            int count;
            while ((count = reader.Read(buffer)) != 0) result.Append(buffer, 0, count);
            Assert.Equal(encoding.GetString(bytes), result.ToString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RotatesOnDiskWithBoundedOddSizedBuffers(bool asynchronous)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Rotate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] original = new byte[200_003], added = new byte[137_021];
            var random = new Random(42); random.NextBytes(original); random.NextBytes(added);
            foreach (int start in new[] { 0, 1, 100_007, original.Length - 1, original.Length })
            {
                string path = Path.Combine(directory, "file");
                File.WriteAllBytes(path, original.Concat(added).ToArray());
                await using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096,
                    asynchronous ? FileOptions.Asynchronous : FileOptions.None))
                {
                    if (asynchronous) await StreamSegments.RotateAsync(stream, start, original.Length, new byte[127], new byte[257]);
                    else StreamSegments.Rotate(stream, start, original.Length, new byte[127], new byte[257]);
                }
                Assert.Equal(original.Take(start).Concat(added).Concat(original.Skip(start)), File.ReadAllBytes(path));
                Assert.Single(Directory.GetFiles(directory));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task AsyncRotationNeverSeeksWithUnflushedBufferedWrites()
    {
        using var stream = new BufferedAsyncStream([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        await StreamSegments.RotateAsync(stream, 2, 6, new byte[2], new byte[3]);
        Assert.Equal(new byte[] { 1, 2, 7, 8, 9, 3, 4, 5, 6 }, stream.Bytes);
    }

    [Fact]
    public void LocatingAnInsertionDoesNotAllocatePerExistingRecord()
    {
        string root = Path.Combine(Path.GetTempPath(), "LinqToFile-ScanAlloc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string small = Path.Combine(root, "small"), large = Path.Combine(root, "large");
            Write(small, 10_000); Write(large, 1_000_000);
            Scan(large);
            long smallBytes = long.MaxValue, largeBytes = long.MaxValue;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                smallBytes = Math.Min(smallBytes, Scan(small));
                largeBytes = Math.Min(largeBytes, Scan(large));
            }
            Assert.InRange(largeBytes - smallBytes, -4096, 4096);
            static void Write(string path, int count)
            {
                using var writer = new StreamWriter(path);
                writer.NewLine = "\n"; writer.WriteLine("Value");
                for (int i = 0; i < count; i++) writer.WriteLine("7");
            }
            static long Scan(string path)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                using var stream = File.OpenRead(path);
                var plan = CsvInsertionPreparation<AllocationRow>.Read(stream, new CsvOptions(), 0);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(6, plan.Offset);
                return allocated;
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class BufferedAsyncStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        private bool dirty = true;
        internal byte[] Bytes => inner.ToArray();
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => true;
        public override long Length => !dirty ? inner.Length : throw new InvalidOperationException("A metadata query could synchronously flush pending data.");
        public override long Position
        {
            get => inner.Position;
            set { if (dirty) throw new InvalidOperationException("A seek could synchronously flush pending data."); inner.Position = value; }
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken); dirty = true;
        }
        public override Task FlushAsync(CancellationToken cancellationToken) { dirty = false; return Task.CompletedTask; }
        public override void Flush() => throw new InvalidOperationException("Sync I/O is forbidden.");
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Sync I/O is forbidden.");
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Sync I/O is forbidden.");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
