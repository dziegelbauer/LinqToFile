using System.Text;

namespace LinqToFile.Reading;

internal static class EncodedTextReader
{
    internal static StreamReader Open(Stream stream, TextOptions options, bool leaveOpen = false, bool preciseReads = false)
    {
        var prefix = new byte[4];
        int count = stream.ReadAtLeast(prefix.AsSpan(), prefix.Length, throwOnEndOfStream: false);
        return Create(stream, prefix, count, options, leaveOpen, preciseReads);
    }

    internal static async ValueTask<StreamReader> OpenAsync(Stream stream, TextOptions options,
        CancellationToken cancellationToken, bool leaveOpen = false, bool preciseReads = false)
    {
        var prefix = new byte[4];
        int count = await stream.ReadAtLeastAsync(prefix.AsMemory(), prefix.Length,
            throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        return Create(stream, prefix, count, options, leaveOpen, preciseReads);
    }

    private static StreamReader Create(Stream stream, byte[] prefix, int count, TextOptions options, bool leaveOpen, bool preciseReads)
    {
        var encoding = SelectEncoding(prefix.AsSpan(0, count), options, out int skip);
        var replay = new PrefixStream(stream, prefix, skip, count, leaveOpen, preciseReads);
        try { return new StreamReader(replay, encoding, detectEncodingFromByteOrderMarks: false); }
        catch { replay.Dispose(); throw; }
    }

    internal static Encoding SelectEncoding(ReadOnlySpan<byte> bytes, TextOptions options, out int skip)
    {
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
        skip = 0;
        if (options.DetectBom && detected is not null)
        {
            encoding = (Encoding)detected.Clone();
            encoding.DecoderFallback = options.Encoding.DecoderFallback;
            encoding.EncoderFallback = options.Encoding.EncoderFallback;
        }
        else if (!options.DetectBom && detected?.CodePage == encoding.CodePage && encoding.GetPreamble().Length == 0)
        {
            skip = detected switch { UTF32Encoding => 4, UTF8Encoding => 3, _ => 2 };
        }
        return encoding;
    }

    private sealed class PrefixStream(Stream inner, byte[] prefix, int position, int count, bool leaveOpen, bool preciseReads) : Stream
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
            if (preciseReads) buffer = buffer[..1];
            int copied = CopyPrefix(buffer);
            return copied != 0 ? copied : inner.Read(buffer);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.IsEmpty) return new(0);
            if (preciseReads) buffer = buffer[..1];
            int copied = CopyPrefix(buffer.Span);
            return copied != 0 ? new(copied) : inner.ReadAsync(buffer, cancellationToken);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private int CopyPrefix(Span<byte> buffer)
        {
            int length = Math.Min(buffer.Length, count - offset);
            prefix.AsSpan(offset, length).CopyTo(buffer);
            offset += length;
            return length;
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
