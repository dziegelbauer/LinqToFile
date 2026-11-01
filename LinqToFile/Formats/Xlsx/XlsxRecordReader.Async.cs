using System.Globalization;
using System.Xml;
using LinqToFile.Infrastructure;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Xlsx;

internal sealed partial class XlsxRecordReader
{
    private async ValueTask PreflightAsync(CancellationToken token)
    {
        using var scan = await package.OpenWorksheetAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await scan.MoveToContentAsync().ConfigureAwait(false);
        if (scan.LocalName != "worksheet" || scan.NamespaceURI != package.SpreadsheetNamespace)
            throw Error("The worksheet root or namespace is invalid.");
        bool dataFound = false;
        bool mergesFound = false;
        bool inMerges = false;
        while (!scan.EOF)
        {
            token.ThrowIfCancellationRequested();
            if (scan.NodeType == XmlNodeType.Element)
            {
                XlsxXml.CheckRequiredMarkup(scan);
                if (scan.Depth == 1 && scan.NamespaceURI == package.SpreadsheetNamespace && scan.LocalName == "sheetData")
                {
                    if (dataFound) throw Error("Duplicate sheet data is not permitted.");
                    dataFound = true;
                }
                if (scan.NamespaceURI == package.SpreadsheetNamespace && scan.LocalName == "mergeCells")
                {
                    if (scan.Depth != 1 || mergesFound) throw Error("Merge declarations are invalid.");
                    mergesFound = true;
                    inMerges = !scan.IsEmptyElement;
                }
                if (scan.NamespaceURI == package.SpreadsheetNamespace && scan.LocalName == "mergeCell")
                {
                    if (scan.Depth != 2 || !inMerges) throw Error("A merge declaration is invalid.");
                    string reference = scan.GetAttribute("ref") ?? throw Error("A merge coordinate is missing.");
                    int colon = reference.IndexOf(':');
                    var first = CellAddress.Parse(colon < 0 ? reference.AsSpan() : reference.AsSpan(0, colon));
                    var last = colon < 0 ? first : CellAddress.Parse(reference.AsSpan(colon + 1));
                    if (first.Row > last.Row || first.Column > last.Column) throw Error("A merge range is reversed.");
                    merges.Add((first, last));
                }
            }
            else if (scan.NodeType == XmlNodeType.EndElement && scan.Depth == 1 && scan.LocalName == "mergeCells") inMerges = false;
            await XlsxXml.ReadAsync(scan, token).ConfigureAwait(false);
        }
        if (!dataFound) throw Error("The worksheet has no sheet data element.");
    }

    private async ValueTask<bool> ReadCoreAsync(CancellationToken token)
    {
        var xml = reader!;
        while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false))
        {
            if (xml.Depth == 1 && xml.NamespaceURI == package.SpreadsheetNamespace && xml.LocalName == "sheetData")
            {
                if (xml.NodeType == XmlNodeType.Element) inData = !xml.IsEmptyElement;
                else if (xml.NodeType == XmlNodeType.EndElement) inData = false;
                continue;
            }
            if (!inData) continue;
            if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
            if (xml.Depth != 2 || xml.NamespaceURI != package.SpreadsheetNamespace || xml.LocalName != "row")
                throw Error("An unsupported element occurs in sheet data.");
            bool content = await ReadRowAsync(xml, token).ConfigureAwait(false);
            int row = checked((int)Row.WorksheetRowNumber!.Value);
            if (header && options.HeaderRow is int selected)
            {
                if (row < selected) continue;
                if (row > selected || !content)
                {
                    Row.WorksheetRowNumber = selected;
                    throw Row.WithLocation(new TabularSchemaException("The selected header row is empty or missing.", 1, 0, 0));
                }
            }
            if (!content) continue;
            Row.RecordNumber = ++recordNumber;
            if (header)
            {
                header = false;
                width = column;
                Row.LimitFields(width);
                foreach (var merge in merges)
                    if (merge.First.Column <= width && merge.Last.Row >= row)
                    {
                        Row.WorksheetRowNumber = Math.Max(row, merge.First.Row);
                        throw Error("A merged range intersects the table.", merge.First.Column);
                    }
            }
            else
            {
                Row.LimitFields(width);
                while (Row.Count < width) Row.EndField();
            }
            return true;
        }
        if (header && options.HeaderRow is int missing)
        {
            Row.WorksheetRowNumber = missing;
            throw Row.WithLocation(new TabularSchemaException("The selected header row is empty or missing.", 1, 0, 0));
        }
        return false;
    }

    private async ValueTask<bool> ReadRowAsync(XmlReader xml, CancellationToken token)
    {
        Row.Reset();
        Row.RecordNumber = recordNumber + 1;
        string? explicitIndex = xml.GetAttribute("r");
        int physicalRow = previousRow + 1;
        if (explicitIndex is not null && (!int.TryParse(explicitIndex, NumberStyles.None, CultureInfo.InvariantCulture, out physicalRow) || physicalRow < 1))
            throw Error("A worksheet row coordinate is invalid.");
        Row.WorksheetRowNumber = physicalRow;
        if (physicalRow <= previousRow || physicalRow > 1_048_576) throw Error("Worksheet rows are duplicated, decreasing, or out of range.");
        int previousColumn = 0;
        int lastContent = 0;
        int depth = xml.Depth;
        if (!xml.IsEmptyElement)
        {
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
                if (xml.Depth != depth + 1 || xml.NamespaceURI != package.SpreadsheetNamespace || xml.LocalName != "c")
                    throw Error("An unsupported element occurs in a worksheet row.");
                XlsxXml.CheckRequiredMarkup(xml);
                string? reference = xml.GetAttribute("r");
                var address = reference is null ? new CellAddress(previousColumn + 1, physicalRow) : CellAddress.Parse(reference);
                if (previousColumn == 0 && explicitIndex is null && reference is not null)
                {
                    physicalRow = address.Row;
                    Row.WorksheetRowNumber = physicalRow;
                    if (physicalRow <= previousRow) throw Error("Worksheet rows are duplicated or decreasing.", address.Column);
                }
                if (address.Row != physicalRow || address.Column <= previousColumn || address.Column > 16384)
                    throw Error("Worksheet cell coordinates are inconsistent or duplicated.", address.Column);
                previousColumn = address.Column;
                bool beforeHeader = header && options.HeaderRow is int selected && physicalRow < selected;
                if (beforeHeader) { await SkipCellAsync(xml, token).ConfigureAwait(false); continue; }
                while (Row.Count < address.Column - 1) Row.EndField();
                bool hasValue = await ReadCellAsync(xml, address.Column, token).ConfigureAwait(false);
                if (hasValue)
                {
                    lastContent = address.Column;
                    if (!header && address.Column > width) throw Error("A value occurs beyond the header range.", address.Column);
                }
            }
        }
        previousRow = physicalRow;
        column = lastContent;
        return lastContent != 0;
    }

    private static async ValueTask SkipCellAsync(XmlReader xml, CancellationToken token)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth)) { }
    }

    private async ValueTask<bool> ReadCellAsync(XmlReader xml, int ordinal, CancellationToken token)
    {
        try { return await ReadCellCoreAsync(xml, ordinal, token).ConfigureAwait(false); }
        catch (XlsxFormatException error) when (error.ColumnIndex is null)
        {
            var contextual = Error(error.Message, ordinal);
            contextual.PartName = error.PartName ?? contextual.PartName;
            throw contextual;
        }
    }

    private async ValueTask<bool> ReadCellCoreAsync(XmlReader xml, int ordinal, CancellationToken token)
    {
        string? type = xml.GetAttribute("t");
        if (type is not (null or "n" or "b" or "inlineStr" or "str" or "e" or "s" or "d"))
            throw Error("The cell type is not supported.", ordinal);
        bool valueFound = false, inlineFound = false, formula = false;
        int depth = xml.Depth;
        if (!xml.IsEmptyElement)
        {
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false) && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
                XlsxXml.CheckRequiredMarkup(xml);
                if (xml.Depth != depth + 1 || xml.NamespaceURI != package.SpreadsheetNamespace)
                    throw Error("The cell contains unsupported markup.", ordinal);
                if (xml.LocalName == "f")
                {
                    if (formula || valueFound || inlineFound) throw Error("The cell has conflicting formula elements.", ordinal);
                    formula = true;
                    await XlsxText.SkipAsync(xml, token).ConfigureAwait(false);
                }
                else if (xml.LocalName == "v")
                {
                    if (valueFound || inlineFound || type == "inlineStr") throw Error("The cell has conflicting value elements.", ordinal);
                    valueFound = true;
                    await text.ReadValueAsync(xml, Row, escaped: type == "str", enforceLimit: type != "s", token: token).ConfigureAwait(false);
                }
                else if (xml.LocalName == "is")
                {
                    if (inlineFound || valueFound || type != "inlineStr" || formula) throw Error("The cell has conflicting inline text.", ordinal);
                    inlineFound = true;
                    await text.ReadRichAsync(xml, Row, token).ConfigureAwait(false);
                }
                else if (xml.LocalName == "extLst") await XlsxText.SkipAsync(xml, token).ConfigureAwait(false);
                else throw Error("The cell contains unsupported markup.", ordinal);
            }
        }
        long? sharedIndex = ResolveSharedIndex(type, formula, valueFound, ordinal);
        string? retained = sharedIndex is long index
            ? await package.SharedStrings!.CopyToAsync(index, Row, token).ConfigureAwait(false) : null;
        return FinishCell(type, formula, ordinal, retained);
    }
}
