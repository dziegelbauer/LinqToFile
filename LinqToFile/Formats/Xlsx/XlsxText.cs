using System.Xml;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

// Reuses the active cell buffer. Every text run is decoded independently, once.
internal sealed class XlsxText(string ns, XlsxOptions options)
{
    private readonly char[] chunk = new char[1024];

    internal void ReadValue(XmlReader xml, TabularRow target, bool escaped, bool enforceLimit = true)
    {
        int start = target.CurrentFieldLength;
        if (!xml.IsEmptyElement)
        {
            int depth = xml.Depth;
            while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace))
                    throw new XlsxFormatException("A cell value contains invalid markup.");
                int count;
                while ((count = xml.ReadValueChunk(chunk, 0, chunk.Length)) != 0)
                {
                    if (enforceLimit && options.MaxCellTextLength is int limit &&
                        (long)target.CurrentFieldLength + count > (escaped ? 7L * limit + 6 : limit))
                        throw new XlsxFormatException("The resolved cell-text limit was exceeded.");
                    target.Append(chunk.AsSpan(0, count));
                }
            }
        }
        if (escaped)
        {
            int decoded = Decode(target.CurrentFieldStorage[start..]);
            target.TruncateCurrentField(start + decoded);
        }
        if (enforceLimit) CheckLength(target.CurrentFieldLength);
    }

    internal void ReadRich(XmlReader xml, TabularRow target)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        bool simple = false, runs = false;
        while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
            XlsxXml.CheckRequiredMarkup(xml);
            if (xml.Depth != depth + 1 || xml.NamespaceURI != ns) throw new XlsxFormatException("Rich text contains unsupported markup.");
            switch (xml.LocalName)
            {
                case "t":
                    if (simple || runs) throw new XlsxFormatException("Rich text has conflicting representations.");
                    simple = true;
                    ReadValue(xml, target, escaped: true);
                    break;
                case "r":
                    if (simple) throw new XlsxFormatException("Rich text has conflicting representations.");
                    runs = true;
                    ReadRun(xml, target);
                    break;
                case "rPh" or "phoneticPr": Skip(xml); break;
                default: throw new XlsxFormatException("Rich text contains unsupported markup.");
            }
        }
    }

    private void ReadRun(XmlReader xml, TabularRow target)
    {
        bool text = false, properties = false;
        int depth = xml.Depth;
        if (!xml.IsEmptyElement)
            while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
                XlsxXml.CheckRequiredMarkup(xml);
                if (xml.Depth != depth + 1 || xml.NamespaceURI != ns) throw new XlsxFormatException("A rich-text run is invalid.");
                if (xml.LocalName == "rPr" && !properties && !text) { properties = true; Skip(xml); }
                else if (xml.LocalName == "t" && !text) { text = true; ReadValue(xml, target, escaped: true); }
                else throw new XlsxFormatException("A rich-text run is invalid.");
            }
        if (!text) throw new XlsxFormatException("A rich-text run has no text element.");
    }

    internal void CheckLength(int length)
    {
        if (options.MaxCellTextLength is int limit && length > limit) throw new XlsxFormatException("The resolved cell-text limit was exceeded.");
    }

    internal static void Skip(XmlReader xml)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            if (xml.NodeType == XmlNodeType.Element) XlsxXml.CheckRequiredMarkup(xml);
    }

    internal static int Decode(Span<char> value)
    {
        int written = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (i + 6 < value.Length && value[i] == '_' && value[i + 1] == 'x' && value[i + 6] == '_' &&
                Hex(value[i + 2]) is int a && a >= 0 && Hex(value[i + 3]) is int b && b >= 0 &&
                Hex(value[i + 4]) is int c && c >= 0 && Hex(value[i + 5]) is int d && d >= 0)
            {
                value[written++] = (char)((a << 12) | (b << 8) | (c << 4) | d);
                i += 6;
            }
            else value[written++] = value[i];
        }
        return written;
    }
    internal static string DecodeMetadata(string value)
    {
        if (!value.Contains("_x", StringComparison.Ordinal)) return value;
        char[] buffer = value.ToCharArray();
        int count = Decode(buffer);
        return count == value.Length ? value : new string(buffer, 0, count);
    }
    private static int Hex(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
}
