using System.Xml;
using System.Globalization;

namespace LinqToFile.Formats.Xlsx;

internal enum XlsxMutationXmlKind : byte { Workbook, Worksheet, SharedStrings }

// The same checks run in sync and async traversal. No rows, cell values, or coordinate lists are retained.
internal sealed class XlsxMutationScan(string ns, XlsxMutationXmlKind kind, bool selected, string part)
{
    internal int LastMergedRow { get; private set; }
    internal bool HasViewCoordinates { get; private set; }
    internal int MaxColumn { get; private set; }
    internal int? StyleCount { get; init; }
    private int currentColumn;
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
            string expected = kind switch
            {
                XlsxMutationXmlKind.Workbook => "workbook",
                XlsxMutationXmlKind.SharedStrings => "sst",
                _ => "worksheet"
            };
            if (xml.Depth != 0 || xml.NamespaceURI != ns || xml.LocalName != expected)
                throw Error("A workbook XML root or namespace is invalid.");
            rootSeen = true;
        }
        if (xml.NamespaceURI != ns) throw Error("Extension markup is not supported for workbook mutation.");
        string name = xml.LocalName;
        if (name == "f") throw Error("Formula cells are not supported for workbook mutation.");
        if (name == "workbookProtection" || selected && name == "sheetProtection")
            throw Error("A protected workbook or selected worksheet cannot be modified.");
        if (!(kind switch
        {
            XlsxMutationXmlKind.Workbook => WorkbookElement(name),
            XlsxMutationXmlKind.SharedStrings => SharedElement(name),
            _ => WorksheetElement(name)
        }))
            throw Error("A workbook feature is not supported for mutation.");
        CheckAttributes(xml, name);
        if (selected && name == "row") currentColumn = 0;
        if (selected && name == "c")
        {
            string? reference = xml.GetAttribute("r");
            currentColumn = reference is null ? currentColumn + 1 : CellAddress.Parse(reference).Column;
            if (currentColumn > 16384) throw Error("A cell coordinate is out of range.");
            MaxColumn = Math.Max(MaxColumn, currentColumn);
            if (xml.GetAttribute("s") is { } style && (!int.TryParse(style, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index < 0 || StyleCount is int count && index >= count)) throw Error("A cell style index is invalid.");
        }
        if (name == "mergeCell")
        {
            string reference = xml.GetAttribute("ref") ?? throw Error("A merge coordinate is missing.");
            int colon = reference.IndexOf(':');
            var first = CellAddress.Parse(colon < 0 ? reference.AsSpan() : reference.AsSpan(0, colon));
            var last = colon < 0 ? first : CellAddress.Parse(reference.AsSpan(colon + 1));
            if (first.Row > last.Row || first.Column > last.Column) throw Error("A merged range is reversed.");
            LastMergedRow = Math.Max(LastMergedRow, last.Row);
            if (selected) MaxColumn = Math.Max(MaxColumn, last.Column);
        }
        if (name is "pane" or "selection" &&
            (xml.GetAttribute("topLeftCell") is not null || xml.GetAttribute("activeCell") is not null || xml.GetAttribute("sqref") is not null))
            HasViewCoordinates = true;
    }

    private void CheckAttributes(XmlReader xml, string element)
    {
        if (!xml.MoveToFirstAttribute()) return;
        do
        {
            string attribute = xml.LocalName;
            string uri = xml.NamespaceURI;
            if (uri == "http://www.w3.org/2000/xmlns/") continue;
            if (uri == "http://www.w3.org/XML/1998/namespace" && attribute is "space" or "lang") continue;
            if (uri is XlsxXml.TransitionalRelationships or XlsxXml.StrictRelationships && attribute == "id" && element is "sheet" or "pageSetup") continue;
            if (uri == "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac" && attribute == "dyDescent" && element is "row" or "sheetFormatPr") continue;
            if (uri == XlsxXml.Compatibility && attribute == "Ignorable")
            {
                foreach (string prefix in xml.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    if (xml.LookupNamespace(prefix) is not (XlsxXml.Transitional or XlsxXml.Strict or XlsxXml.TransitionalRelationships or
                        XlsxXml.StrictRelationships or "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac"))
                        throw Error("An ignored extension namespace is not supported for mutation.");
                continue;
            }
            if (uri.Length != 0 || !Attribute(element, attribute)) throw Error("A workbook attribute is not supported for mutation.");
        } while (xml.MoveToNextAttribute());
        xml.MoveToElement();
    }

    private static bool Attribute(string element, string attribute) => element switch
    {
        "workbook" => attribute == "conformance",
        "fileVersion" => attribute is "appName" or "lastEdited" or "lowestEdited" or "rupBuild" or "codeName",
        "workbookPr" => attribute is "date1904" or "showObjects" or "showBorderUnselectedTables" or "filterPrivacy" or "promptedSolutions" or
            "showInkAnnotation" or "backupFile" or "saveExternalLinkValues" or "updateLinks" or "codeName" or "hidePivotFieldList" or
            "showPivotChartFilter" or "allowRefreshQuery" or "publishItems" or "checkCompatibility" or "autoCompressPictures" or "refreshAllConnections" or "defaultThemeVersion",
        "workbookView" => attribute is "visibility" or "minimized" or "showHorizontalScroll" or "showVerticalScroll" or "showSheetTabs" or
            "xWindow" or "yWindow" or "windowWidth" or "windowHeight" or "tabRatio" or "firstSheet" or "activeTab" or "autoFilterDateGrouping",
        "sheet" => attribute is "name" or "sheetId" or "state",
        "calcPr" => attribute is "calcId" or "calcMode" or "fullCalcOnLoad" or "refMode" or "iterate" or "iterateCount" or "iterateDelta" or
            "fullPrecision" or "calcCompleted" or "calcOnSave" or "concurrentCalc" or "concurrentManualCount" or "forceFullCalc",
        "sheetPr" => attribute is "syncHorizontal" or "syncVertical" or "syncRef" or "transitionEvaluation" or "transitionEntry" or "published" or "codeName" or "filterMode" or "enableFormatConditionsCalculation",
        "tabColor" or "color" => attribute is "auto" or "indexed" or "rgb" or "theme" or "tint",
        "outlinePr" => attribute is "applyStyles" or "summaryBelow" or "summaryRight" or "showOutlineSymbols",
        "pageSetUpPr" => attribute is "autoPageBreaks" or "fitToPage",
        "dimension" => attribute == "ref",
        "sheetView" => attribute is "windowProtection" or "showFormulas" or "showGridLines" or "showRowColHeaders" or "showZeros" or "rightToLeft" or
            "tabSelected" or "showRuler" or "showOutlineSymbols" or "defaultGridColor" or "showWhiteSpace" or "view" or "topLeftCell" or "colorId" or
            "zoomScale" or "zoomScaleNormal" or "zoomScaleSheetLayoutView" or "zoomScalePageLayoutView" or "workbookViewId" or "zoomToFit",
        "pane" => attribute is "xSplit" or "ySplit" or "topLeftCell" or "activePane" or "state",
        "selection" => attribute is "pane" or "activeCell" or "activeCellId" or "sqref",
        "sheetFormatPr" => attribute is "baseColWidth" or "defaultColWidth" or "defaultRowHeight" or "customHeight" or "zeroHeight" or "thickTop" or "thickBottom" or "outlineLevelRow" or "outlineLevelCol",
        "col" => attribute is "min" or "max" or "width" or "style" or "hidden" or "bestFit" or "customWidth" or "phonetic" or "outlineLevel" or "collapsed",
        "row" => attribute is "r" or "spans" or "s" or "customFormat" or "ht" or "hidden" or "customHeight" or "outlineLevel" or "collapsed" or "thickTop" or "thickBot" or "ph",
        "c" => attribute is "r" or "s" or "t" or "ph",
        "sst" => attribute is "count" or "uniqueCount",
        "rFont" or "charset" or "family" or "b" or "i" or "strike" or "outline" or "shadow" or "condense" or "extend" or "sz" or "u" or "vertAlign" or "scheme" => attribute == "val",
        "rPh" => attribute is "sb" or "eb",
        "phoneticPr" => attribute is "fontId" or "type" or "alignment",
        "mergeCells" => attribute == "count",
        "mergeCell" => attribute == "ref",
        "printOptions" => attribute is "horizontalCentered" or "verticalCentered" or "headings" or "gridLines" or "gridLinesSet",
        "pageMargins" => attribute is "left" or "right" or "top" or "bottom" or "header" or "footer",
        "pageSetup" => attribute is "paperSize" or "scale" or "firstPageNumber" or "fitToWidth" or "fitToHeight" or "pageOrder" or "orientation" or
            "usePrinterDefaults" or "blackAndWhite" or "draft" or "cellComments" or "useFirstPageNumber" or "errors" or "horizontalDpi" or "verticalDpi" or "copies",
        "headerFooter" => attribute is "differentOddEven" or "differentFirst" or "scaleWithDoc" or "alignWithMargins",
        "sheetProtection" => true, // Preserved only on untouched sheets; selected protection is rejected before this point.
        _ => false
    };

    private XlsxFormatException Error(string message) => XlsxPackage.Error(message, part);

    private static bool WorkbookElement(string name) => name is
        "workbook" or "fileVersion" or "workbookPr" or "bookViews" or "workbookView" or "sheets" or "sheet" or "calcPr";

    private static bool SharedElement(string name) => name is "sst" or "si" or "t" or "r" or "rPr" or "rPh" or "phoneticPr" or
        "rFont" or "charset" or "family" or "b" or "i" or "strike" or "outline" or "shadow" or "condense" or "extend" or
        "color" or "sz" or "u" or "vertAlign" or "scheme";

    private static bool WorksheetElement(string name) => name is
        "worksheet" or "sheetPr" or "tabColor" or "outlinePr" or "pageSetUpPr" or "dimension" or
        "sheetViews" or "sheetView" or "pane" or "selection" or "sheetFormatPr" or "cols" or "col" or
        "sheetData" or "row" or "c" or "v" or "is" or "t" or "r" or "rPr" or "rFont" or
        "charset" or "family" or "b" or "i" or "strike" or "outline" or "shadow" or "condense" or
        "extend" or "color" or "sz" or "u" or "vertAlign" or "scheme" or "rPh" or "phoneticPr" or
        "sheetProtection" or "mergeCells" or "mergeCell" or "printOptions" or "pageMargins" or "pageSetup" or
        "headerFooter" or "oddHeader" or "oddFooter" or "evenHeader" or "evenFooter" or "firstHeader" or "firstFooter";
}
