using System.Text;

namespace LinqToFile.Reading;

// Text decoding is shared by delimited formats, not imposed on binary/package formats.
internal abstract record TextOptions : TabularOptions
{
    internal Encoding Encoding { get; init; } = new UTF8Encoding(false, true);
    internal bool DetectBom { get; init; } = true;
    internal int BufferSize { get; init; } = 4096;
}
