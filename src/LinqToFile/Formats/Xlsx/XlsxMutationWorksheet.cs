using System.Globalization;
using System.Xml;

namespace LinqToFile.Formats.Xlsx;

// Copies tokens, not mapped values. Only row/cell coordinates and the used-range hint are changed.
internal sealed class XlsxMutationWorksheet(int insertionRow, long count, int lastRow, int maxColumn, Stream spool, Func<ValueTask>? drain = null)
{
    private readonly char[] text = new char[4096];
    private readonly char[] coordinate = new char[32];
    private bool inserted;
    private int previousRow;

    internal void Copy(XmlReader xml, XmlWriter writer)
    {
        while (!xml.EOF)
        {
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == 2 && xml.LocalName == "row")
            { CopyRow(xml, writer); continue; }
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == 1 && xml.LocalName == "dimension")
            {
                writer.WriteStartElement(xml.Prefix, xml.LocalName, xml.NamespaceURI);
                writer.WriteAttributeString("ref", "A1:" + Address(maxColumn, checked(lastRow + count)));
                writer.WriteEndElement(); xml.Skip(); continue;
            }
            if (xml.Depth == 1 && xml.LocalName == "sheetData")
            {
                if (xml.NodeType == XmlNodeType.EndElement) Insert(writer);
                else if (xml.NodeType == XmlNodeType.Element && xml.IsEmptyElement)
                {
                    writer.WriteStartElement(xml.Prefix, xml.LocalName, xml.NamespaceURI); Attributes(xml, writer, null, false);
                    Insert(writer); writer.WriteFullEndElement(); xml.Read(); continue;
                }
            }
            Token(xml, writer, null);
            xml.Read();
        }
    }

    internal async ValueTask CopyAsync(XmlReader xml, XmlWriter writer, CancellationToken token)
    {
        while (!xml.EOF)
        {
            token.ThrowIfCancellationRequested();
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == 2 && xml.LocalName == "row")
            {
                await CopyRowAsync(xml, writer, token).ConfigureAwait(false);
                if (drain is not null) await drain().ConfigureAwait(false);
                continue;
            }
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == 1 && xml.LocalName == "dimension")
            {
                await writer.WriteStartElementAsync(xml.Prefix, xml.LocalName, xml.NamespaceURI).ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "ref", null, "A1:" + Address(maxColumn, checked(lastRow + count))).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false); await xml.SkipAsync().ConfigureAwait(false); continue;
            }
            if (xml.Depth == 1 && xml.LocalName == "sheetData")
            {
                if (xml.NodeType == XmlNodeType.EndElement) await InsertAsync(writer, token).ConfigureAwait(false);
                else if (xml.NodeType == XmlNodeType.Element && xml.IsEmptyElement)
                {
                    await writer.WriteStartElementAsync(xml.Prefix, xml.LocalName, xml.NamespaceURI).ConfigureAwait(false);
                    await AttributesAsync(xml, writer, null, false).ConfigureAwait(false);
                    await InsertAsync(writer, token).ConfigureAwait(false);
                    await writer.WriteFullEndElementAsync().ConfigureAwait(false); await xml.ReadAsync().ConfigureAwait(false); continue;
                }
            }
            await TokenAsync(xml, writer, null, token).ConfigureAwait(false);
            await xml.ReadAsync().ConfigureAwait(false);
        }
    }

    private void CopyRow(XmlReader xml, XmlWriter writer)
    {
        string prefix = xml.Prefix, ns = xml.NamespaceURI;
        string? explicitRow = xml.GetAttribute("r");
        int row = explicitRow is null ? previousRow + 1 : int.Parse(explicitRow, CultureInfo.InvariantCulture);
        bool empty = xml.IsEmptyElement;
        List<SavedAttribute>? saved = null;
        if (explicitRow is null && !empty)
        {
            saved = SaveAttributes(xml);
            while (xml.Read() && xml.NodeType is XmlNodeType.Whitespace or XmlNodeType.Comment) { }
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "c" && xml.GetAttribute("r") is { } reference)
                row = CellAddress.Parse(reference).Row;
        }
        if (row >= insertionRow) Insert(writer);
        long shifted = row >= insertionRow ? row + count : row;
        writer.WriteStartElement(prefix, "row", ns);
        if (saved is null) Attributes(xml, writer, shifted, true);
        else
        {
            foreach (var item in saved) writer.WriteAttributeString(item.Prefix, item.Name, item.Namespace, item.Value);
            CoordinateAttribute(writer, shifted, null);
        }
        if (empty) { writer.WriteEndElement(); xml.Read(); previousRow = row; return; }
        if (saved is null) xml.Read();
        while (!(xml.NodeType == XmlNodeType.EndElement && xml.Depth == 2 && xml.LocalName == "row"))
        {
            if (xml.EOF) throw new XlsxFormatException("A worksheet row is incomplete.");
            Token(xml, writer, shifted); xml.Read();
        }
        writer.WriteFullEndElement(); xml.Read(); previousRow = row;
    }

    private async ValueTask CopyRowAsync(XmlReader xml, XmlWriter writer, CancellationToken token)
    {
        string prefix = xml.Prefix, ns = xml.NamespaceURI;
        string? explicitRow = xml.GetAttribute("r");
        int row = explicitRow is null ? previousRow + 1 : int.Parse(explicitRow, CultureInfo.InvariantCulture);
        bool empty = xml.IsEmptyElement;
        List<SavedAttribute>? saved = null;
        if (explicitRow is null && !empty)
        {
            saved = SaveAttributes(xml);
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && xml.NodeType is XmlNodeType.Whitespace or XmlNodeType.Comment) { }
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "c" && xml.GetAttribute("r") is { } reference)
                row = CellAddress.Parse(reference).Row;
        }
        if (row >= insertionRow) await InsertAsync(writer, token).ConfigureAwait(false);
        long shifted = row >= insertionRow ? row + count : row;
        await writer.WriteStartElementAsync(prefix, "row", ns).ConfigureAwait(false);
        if (saved is null) await AttributesAsync(xml, writer, shifted, true).ConfigureAwait(false);
        else
        {
            foreach (var item in saved) await writer.WriteAttributeStringAsync(item.Prefix, item.Name, item.Namespace, item.Value).ConfigureAwait(false);
            await CoordinateAttributeAsync(writer, shifted, null).ConfigureAwait(false);
        }
        if (empty) { await writer.WriteEndElementAsync().ConfigureAwait(false); await xml.ReadAsync().ConfigureAwait(false); previousRow = row; return; }
        if (saved is null) await xml.ReadAsync().ConfigureAwait(false);
        while (!(xml.NodeType == XmlNodeType.EndElement && xml.Depth == 2 && xml.LocalName == "row"))
        {
            token.ThrowIfCancellationRequested();
            if (xml.EOF) throw new XlsxFormatException("A worksheet row is incomplete.");
            await TokenAsync(xml, writer, shifted, token).ConfigureAwait(false); await xml.ReadAsync().ConfigureAwait(false);
        }
        await writer.WriteFullEndElementAsync().ConfigureAwait(false); await xml.ReadAsync().ConfigureAwait(false); previousRow = row;
    }

    private void Token(XmlReader xml, XmlWriter writer, long? row)
    {
        switch (xml.NodeType)
        {
            case XmlNodeType.Element:
                writer.WriteStartElement(xml.Prefix, xml.LocalName, xml.NamespaceURI);
                Attributes(xml, writer, xml.Depth == 3 && xml.LocalName == "c" ? row : null, false);
                if (xml.IsEmptyElement) writer.WriteEndElement();
                break;
            case XmlNodeType.EndElement: writer.WriteFullEndElement(); break;
            case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace:
                if (xml.CanReadValueChunk) { int read; while ((read = xml.ReadValueChunk(text, 0, text.Length)) != 0) writer.WriteChars(text, 0, read); }
                else writer.WriteString(xml.Value);
                break;
            case XmlNodeType.Whitespace: writer.WriteWhitespace(xml.Value); break;
            case XmlNodeType.Comment: writer.WriteComment(xml.Value); break;
            case XmlNodeType.ProcessingInstruction: writer.WriteProcessingInstruction(xml.Name, xml.Value); break;
        }
    }

    private async ValueTask TokenAsync(XmlReader xml, XmlWriter writer, long? row, CancellationToken token)
    {
        switch (xml.NodeType)
        {
            case XmlNodeType.Element:
                await writer.WriteStartElementAsync(xml.Prefix, xml.LocalName, xml.NamespaceURI).ConfigureAwait(false);
                await AttributesAsync(xml, writer, xml.Depth == 3 && xml.LocalName == "c" ? row : null, false).ConfigureAwait(false);
                if (xml.IsEmptyElement) await writer.WriteEndElementAsync().ConfigureAwait(false);
                break;
            case XmlNodeType.EndElement: await writer.WriteFullEndElementAsync().ConfigureAwait(false); break;
            case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace:
                if (xml.CanReadValueChunk)
                {
                    int read;
                    while ((read = await xml.ReadValueChunkAsync(text, 0, text.Length).ConfigureAwait(false)) != 0)
                    { token.ThrowIfCancellationRequested(); await writer.WriteCharsAsync(text, 0, read).ConfigureAwait(false); }
                }
                else await writer.WriteStringAsync(xml.Value).ConfigureAwait(false);
                break;
            case XmlNodeType.Whitespace: await writer.WriteWhitespaceAsync(xml.Value).ConfigureAwait(false); break;
            case XmlNodeType.Comment: await writer.WriteCommentAsync(xml.Value).ConfigureAwait(false); break;
            case XmlNodeType.ProcessingInstruction: await writer.WriteProcessingInstructionAsync(xml.Name, xml.Value).ConfigureAwait(false); break;
        }
    }

    private void Attributes(XmlReader xml, XmlWriter writer, long? row, bool isRow)
    {
        bool seen = false;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                if (row is long number && xml.LocalName == "r" && xml.NamespaceURI.Length == 0)
                { CoordinateAttribute(writer, number, isRow ? null : xml.Value); seen = true; }
                else writer.WriteAttributeString(xml.Prefix, xml.LocalName, xml.NamespaceURI, xml.Value);
            } while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        if (isRow && !seen) CoordinateAttribute(writer, row!.Value, null);
    }

    private async ValueTask AttributesAsync(XmlReader xml, XmlWriter writer, long? row, bool isRow)
    {
        bool seen = false;
        if (xml.MoveToFirstAttribute())
        {
            do
            {
                if (row is long number && xml.LocalName == "r" && xml.NamespaceURI.Length == 0)
                { await CoordinateAttributeAsync(writer, number, isRow ? null : xml.Value).ConfigureAwait(false); seen = true; }
                else await writer.WriteAttributeStringAsync(xml.Prefix, xml.LocalName, xml.NamespaceURI, xml.Value).ConfigureAwait(false);
            } while (xml.MoveToNextAttribute());
            xml.MoveToElement();
        }
        if (isRow && !seen) await CoordinateAttributeAsync(writer, row!.Value, null).ConfigureAwait(false);
    }

    private int FormatCoordinate(long row, string? original)
    {
        int prefix = 0;
        if (original is not null) while (prefix < original.Length && original[prefix] is >= 'A' and <= 'Z') { coordinate[prefix] = original[prefix]; prefix++; }
        row.TryFormat(coordinate.AsSpan(prefix), out int length, provider: CultureInfo.InvariantCulture);
        return prefix + length;
    }
    private void CoordinateAttribute(XmlWriter writer, long row, string? original)
    {
        int length = FormatCoordinate(row, original);
        writer.WriteStartAttribute("r"); writer.WriteChars(coordinate, 0, length); writer.WriteEndAttribute();
    }
    private async ValueTask CoordinateAttributeAsync(XmlWriter writer, long row, string? original)
    {
        int length = FormatCoordinate(row, original);
        // XmlWriter's async attribute-boundary methods are protected. These small state transitions
        // can emit buffered bytes; the forwarding view captures them and is drained after each row.
        writer.WriteStartAttribute("r");
        await writer.WriteCharsAsync(coordinate, 0, length).ConfigureAwait(false);
        writer.WriteEndAttribute();
    }
    private void Insert(XmlWriter writer)
    {
        if (inserted) return;
        using var input = new StreamReader(spool, XlsxDocument.Utf8, false, 4096, true);
        int read;
        while ((read = input.Read(text, 0, text.Length)) != 0) writer.WriteRaw(text, 0, read);
        inserted = true;
    }
    private async ValueTask InsertAsync(XmlWriter writer, CancellationToken token)
    {
        if (inserted) return;
        using var input = new StreamReader(spool, XlsxDocument.Utf8, false, 4096, true);
        int read;
        while ((read = await input.ReadAsync(text.AsMemory(), token).ConfigureAwait(false)) != 0)
            await writer.WriteRawAsync(text, 0, read).ConfigureAwait(false);
        inserted = true;
    }
    private static string Address(int column, long row)
    {
        Span<char> buffer = stackalloc char[32];
        int start = 3;
        while (column > 0) { buffer[--start] = (char)('A' + (column - 1) % 26); column = (column - 1) / 26; }
        row.TryFormat(buffer[3..], out int length, provider: CultureInfo.InvariantCulture);
        return new string(buffer.Slice(start, 3 - start + length));
    }
    private static List<SavedAttribute> SaveAttributes(XmlReader xml)
    {
        var result = new List<SavedAttribute>();
        if (!xml.MoveToFirstAttribute()) return result;
        do { result.Add(new(xml.Prefix, xml.LocalName, xml.NamespaceURI, xml.Value)); } while (xml.MoveToNextAttribute());
        xml.MoveToElement(); return result;
    }
    private readonly record struct SavedAttribute(string Prefix, string Name, string Namespace, string Value);
}
