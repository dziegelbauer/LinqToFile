using System.Text;

namespace LinqToFile;

internal static class CsvTextReader
{
    // Phase 1 accepts paths only, so this reader can inspect and rewind a FileStream.
    // A later stream-source implementation must preserve this policy without requiring seeks.
    internal static StreamReader Open(FileStream stream, Encoding configured)
    {
        Span<byte> prefix = stackalloc byte[4];
        int count = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        var bytes = prefix[..count];
        Encoding? detected = null;
        int skip = 0;
        if (bytes is [0x00, 0x00, 0xFE, 0xFF, ..])
        {
            detected = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            skip = 4;
        }
        else if (bytes is [0xFF, 0xFE, 0x00, 0x00, ..])
        {
            detected = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
            skip = 4;
        }
        else if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            detected = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            skip = 3;
        }
        else if (bytes is [0xFE, 0xFF, ..])
        {
            detected = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            skip = 2;
        }
        else if (bytes is [0xFF, 0xFE, ..])
        {
            detected = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            skip = 2;
        }

        stream.Position = skip;
        var encoding = configured;
        if (detected is not null)
        {
            encoding = (Encoding)detected.Clone();
            encoding.DecoderFallback = configured.DecoderFallback;
        }
        return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false);
    }
}
