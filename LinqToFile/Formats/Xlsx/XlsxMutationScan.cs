using System.Xml;

namespace LinqToFile.Formats.Xlsx;

internal enum XlsxMutationXmlKind : byte { Workbook, Worksheet }

// The same checks run in sync and async traversal. No rows, cell values, or coordinate lists are retained.
internal sealed class XlsxMutationScan(string ns, XlsxMutationXmlKind kind, bool selected, string part)
{
    internal int LastMergedRow { get; private set; }
    internal bool HasViewCoordinates { get; private set; }
    private bool rootSeen;

    internal void Read(XmlReader reader)
    {
        try
        {
            while (reader.Read()) Check(reader);
            Finish();
        }
        catch (XmlException) { throw Error("A workbook XML part is malformed."); }
        catch (InvalidDataException) { throw Error("A workbook XML part could not be decompressed."); }
    }

    internal async ValueTask ReadAsync(XmlReader reader, CancellationToken token)
    {
        try
        {
            while (await XlsxXml.ReadAsync(reader, token).ConfigureAwait(false)) Check(reader);
            Finish();
        }
        catch (XmlException) { throw Error("A workbook XML part is malformed."); }
        catch (InvalidDataException) { throw Error("A workbook XML part could not be decompressed."); }
    }

    private void Finish()
    {
        if (!rootSeen) throw Error("A workbook XML root is missing.");
    }

    private void Check(XmlReader xml)
    {
        if (xml.NodeType != XmlNodeType.Element) return;
        XlsxXml.CheckRequiredMarkup(xml);
        if (!rootSeen)
        {
            string expected = kind == XlsxMutationXmlKind.Workbook ? "workbook" : "worksheet";
            if (xml.Depth != 0 || xml.NamespaceURI != ns || xml.LocalName != expected)
                throw Error("A workbook XML root or namespace is invalid.");
            rootSeen = true;
        }
        if (xml.NamespaceURI != ns) throw Error("Extension markup is not supported for workbook mutation.");
        string name = xml.LocalName;
        if (name == "f") throw Error("Formula cells are not supported for workbook mutation.");
        if (name == "workbookProtection" || selected && name == "sheetProtection")
            throw Error("A protected workbook or selected worksheet cannot be modified.");
        if (!(kind == XlsxMutationXmlKind.Workbook ? WorkbookElement(name) : WorksheetElement(name)))
            throw Error("A workbook feature is not supported for mutation.");
        if (name == "mergeCell")
        {
            string reference = xml.GetAttribute("ref") ?? throw Error("A merge coordinate is missing.");
            int colon = reference.IndexOf(':');
            var first = CellAddress.Parse(colon < 0 ? reference.AsSpan() : reference.AsSpan(0, colon));
            var last = colon < 0 ? first : CellAddress.Parse(reference.AsSpan(colon + 1));
            if (first.Row > last.Row || first.Column > last.Column) throw Error("A merged range is reversed.");
            LastMergedRow = Math.Max(LastMergedRow, last.Row);
        }
        if (name is "pane" or "selection" &&
            (xml.GetAttribute("topLeftCell") is not null || xml.GetAttribute("activeCell") is not null || xml.GetAttribute("sqref") is not null))
            HasViewCoordinates = true;
    }

    private XlsxFormatException Error(string message) => XlsxPackage.Error(message, part);

    private static bool WorkbookElement(string name) => name is
        "workbook" or "fileVersion" or "workbookPr" or "bookViews" or "workbookView" or "sheets" or "sheet" or "calcPr";

    private static bool WorksheetElement(string name) => name is
        "worksheet" or "sheetPr" or "tabColor" or "outlinePr" or "pageSetUpPr" or "dimension" or
        "sheetViews" or "sheetView" or "pane" or "selection" or "sheetFormatPr" or "cols" or "col" or
        "sheetData" or "row" or "c" or "v" or "is" or "t" or "r" or "rPr" or "rFont" or
        "charset" or "family" or "b" or "i" or "strike" or "outline" or "shadow" or "condense" or
        "extend" or "color" or "sz" or "u" or "vertAlign" or "scheme" or "rPh" or "phoneticPr" or
        "sheetProtection" or "mergeCells" or "mergeCell" or "printOptions" or "pageMargins" or "pageSetup" or
        "headerFooter" or "oddHeader" or "oddFooter" or "evenHeader" or "evenFooter" or "firstHeader" or "firstFooter";
}
