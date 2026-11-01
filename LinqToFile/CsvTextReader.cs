using System.Text;

namespace LinqToFile;

internal static class CsvTextReader
{
    internal static StreamReader Open(Stream stream, CsvOptions options, bool leaveOpen = false)
    {
        // Retain at most four prefix bytes. The input does not need Position, Length, or Seek.
        var prefix = new byte[4];
        int count = stream.ReadAtLeast(prefix.AsSpan(), prefix.Length, throwOnEndOfStream: false);
        var bytes = prefix.AsSpan(0, count);
        Encoding? detected = bytes switch
        {
            [0x00, 0x00, 0xFE, 0xFF, ..] => new UTF32Encoding(true, true),
            [0xFF, 0xFE, 0x00, 0x00, ..] => new UTF32Encoding(false, true),
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(true),
            [0xFE, 0xFF, ..] => new UnicodeEncoding(true, true),
            [0xFF, 0xFE, ..] => new UnicodeEncoding(false, true),
            _ => null
        };

        var encoding = options.Encoding;
        int skip = 0;
        if (options.DetectBom && detected is not null)
        {
            encoding = (Encoding)detected.Clone();
            encoding.DecoderFallback = options.Encoding.DecoderFallback;
        }
        else if (!options.DetectBom && detected?.CodePage == encoding.CodePage && encoding.GetPreamble().Length == 0)
        {
            // Even a no-preamble UTF encoding can remove its matching input BOM.
            skip = detected switch
            {
                UTF32Encoding => 4,
                UTF8Encoding => 3,
                _ => 2
            };
        }

        // StreamReader removes the selected encoding's matching preamble, but never changes encoding.
        var replay = new PrefixStream(stream, prefix, skip, count, leaveOpen);
        try
        {
            return new StreamReader(replay, encoding, detectEncodingFromByteOrderMarks: false);
        }
        catch
        {
            replay.Dispose();
            throw;
        }
    }

    private sealed class PrefixStream(Stream inner, byte[] prefix, int position, int count, bool leaveOpen) : Stream
    {
        private int offset = position;
        private bool disposed;
        public override bool CanRead => !disposed && inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (buffer.IsEmpty) return 0;
            if (offset < count)
            {
                int length = Math.Min(buffer.Length, count - offset);
                prefix.AsSpan(offset, length).CopyTo(buffer);
                offset += length;
                return length;
            }
            return inner.Read(buffer);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!disposed && disposing && !leaveOpen) inner.Dispose();
            disposed = true;
            base.Dispose(disposing);
        }
    }
}
