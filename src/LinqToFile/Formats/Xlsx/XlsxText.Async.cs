using System.Xml;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

internal sealed partial class XlsxText
{
    internal async ValueTask ReadValueAsync(XmlReader xml, TabularRow target, bool escaped, bool enforceLimit = true, CancellationToken token = default)
    {
        int start = target.CurrentFieldLength;
        if (!xml.IsEmptyElement)
        {
            int depth = xml.Depth;
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace))
                    throw new XlsxFormatException("A cell value contains invalid markup.");
                int count;
                while ((count = await xml.ReadValueChunkAsync(chunk, 0, chunk.Length).ConfigureAwait(false)) != 0)
                {
                    if (enforceLimit && options.MaxCellTextLength is int limit &&
                        (long)target.CurrentFieldLength + count > (escaped ? 7L * limit + 6 : limit))
                        throw new XlsxFormatException("The resolved cell-text limit was exceeded.");
                    token.ThrowIfCancellationRequested();
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

    internal async ValueTask ReadRichAsync(XmlReader xml, TabularRow target, CancellationToken token)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        bool simple = false, runs = false;
        while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
            XlsxXml.CheckRequiredMarkup(xml);
            if (xml.Depth != depth + 1 || xml.NamespaceURI != ns) throw new XlsxFormatException("Rich text contains unsupported markup.");
            switch (xml.LocalName)
            {
                case "t":
                    if (simple || runs) throw new XlsxFormatException("Rich text has conflicting representations.");
                    simple = true;
                    await ReadValueAsync(xml, target, escaped: true, token: token).ConfigureAwait(false);
                    break;
                case "r":
                    if (simple) throw new XlsxFormatException("Rich text has conflicting representations.");
                    runs = true;
                    await ReadRunAsync(xml, target, token).ConfigureAwait(false);
                    break;
                case "rPh" or "phoneticPr": await SkipAsync(xml, token).ConfigureAwait(false); break;
                default: throw new XlsxFormatException("Rich text contains unsupported markup.");
            }
        }
    }

    private async ValueTask ReadRunAsync(XmlReader xml, TabularRow target, CancellationToken token)
    {
        bool text = false, properties = false;
        int depth = xml.Depth;
        if (!xml.IsEmptyElement)
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
                XlsxXml.CheckRequiredMarkup(xml);
                if (xml.Depth != depth + 1 || xml.NamespaceURI != ns) throw new XlsxFormatException("A rich-text run is invalid.");
                if (xml.LocalName == "rPr" && !properties && !text) { properties = true; await SkipAsync(xml, token).ConfigureAwait(false); }
                else if (xml.LocalName == "t" && !text) { text = true; await ReadValueAsync(xml, target, escaped: true, token: token).ConfigureAwait(false); }
                else throw new XlsxFormatException("A rich-text run is invalid.");
            }
        if (!text) throw new XlsxFormatException("A rich-text run has no text element.");
    }

    internal static async ValueTask SkipAsync(XmlReader xml, CancellationToken token)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            if (xml.NodeType == XmlNodeType.Element) XlsxXml.CheckRequiredMarkup(xml);
    }
}
