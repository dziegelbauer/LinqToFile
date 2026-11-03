using System.Globalization;
using System.Xml.Linq;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

// Existing style indexes never change. At most one style per native temporal kind is added.
internal sealed class XlsxMutationStyles
{
    private readonly string ns;
    private readonly int[] selected = [-1, -1, -1];
    internal XElement? Root { get; private set; }
    internal bool Modified { get; private set; }
    internal int OriginalCount { get; }

    internal XlsxMutationStyles(XElement? root, string ns)
    {
        this.ns = ns;
        Root = root;
        if (root is null) { OriginalCount = 1; return; }
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName != ns || !Known(element.Name.LocalName))
                throw new XlsxFormatException("Styles contain unsupported extension markup.");
            foreach (var attribute in element.Attributes())
                if (!attribute.IsNamespaceDeclaration && (attribute.Name.NamespaceName.Length != 0 || !KnownAttribute(attribute.Name.LocalName)))
                    throw new XlsxFormatException("Styles contain unsupported attributes.");
        }
        foreach (string collection in new[] { "numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs", "cellStyles", "dxfs" })
        {
            var containers = root.Elements(XName.Get(collection, ns)).ToArray();
            if (containers.Length > 1) throw new XlsxFormatException("Duplicate style collections are invalid.");
            if (containers.Length == 1 && containers[0].Attribute("count") is { } count &&
                (!int.TryParse(count.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int total) || total != containers[0].Elements().Count()))
                throw new XlsxFormatException("A style collection count is invalid.");
        }
        OriginalCount = root.Element(XName.Get("cellXfs", ns))?.Elements().Count() ?? 0;
        if (OriginalCount is < 1 or > 65490) throw new XlsxFormatException("The cell-style collection is missing or too large.");
        var ids = new HashSet<int>();
        foreach (var format in root.Element(XName.Get("numFmts", ns))?.Elements() ?? [])
            if (!int.TryParse((string?)format.Attribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                id is < 164 or > 65535 || !ids.Add(id) || format.Attribute("formatCode") is null)
                throw new XlsxFormatException("A custom number format is invalid or duplicated.");
    }

    internal int Get(ExportCellKind kind)
    {
        int slot = kind == ExportCellKind.Date ? 0 : kind == ExportCellKind.DateTime ? 1 : 2;
        if (selected[slot] >= 0) return selected[slot];
        if (Root is null)
        {
            Root = XElement.Parse(XlsxDocument.Parts(new XlsxWriteOptions()).Single(p => p.Name == "xl/styles.xml").Text);
            foreach (var element in Root.DescendantsAndSelf())
            {
                element.Attributes().Where(a => a.IsNamespaceDeclaration && a.Value == XlsxXml.Transitional).Remove();
                element.Name = XName.Get(element.Name.LocalName, ns);
            }
            selected[0] = 1; selected[1] = 2; selected[2] = 3;
            Modified = true;
            return selected[slot];
        }
        string code = slot == 0 ? "yyyy-mm-dd" : slot == 1 ? "yyyy-mm-dd hh:mm:ss.000" : "hh:mm:ss.000";
        XName N(string local) => XName.Get(local, ns);
        var formats = Root.Element(N("numFmts"));
        int formatId = -1;
        int nextFormat = 164;
        if (formats is not null)
            foreach (var format in formats.Elements())
            {
                int id = int.Parse(format.Attribute("numFmtId")!.Value, CultureInfo.InvariantCulture);
                nextFormat = Math.Max(nextFormat, checked(id + 1));
                if ((string?)format.Attribute("formatCode") == code) formatId = id;
            }
        var styles = Root.Element(N("cellXfs"))!;
        string? formattedId = formatId < 0 ? null : formatId.ToString(CultureInfo.InvariantCulture);
        int index = 0;
        foreach (var style in styles.Elements())
        {
            if (formattedId is not null && (string?)style.Attribute("numFmtId") == formattedId &&
                !style.HasElements && ((string?)style.Attribute("fontId") is null or "0") &&
                ((string?)style.Attribute("fillId") is null or "0") && ((string?)style.Attribute("borderId") is null or "0") &&
                ((string?)style.Attribute("applyNumberFormat") is "1" or "true"))
                return selected[slot] = index;
            index++;
        }
        if (index >= 65490) throw new XlsxFormatException("The cell-style limit would be exceeded.");
        if (Root.Element(N("fonts"))?.HasElements != true || Root.Element(N("fills"))?.HasElements != true ||
            Root.Element(N("borders"))?.HasElements != true || Root.Element(N("cellStyleXfs"))?.HasElements != true)
            throw new XlsxFormatException("The base style collections required for native output are missing.");
        if (formatId < 0)
        {
            if (nextFormat > 65535) throw new XlsxFormatException("The custom number-format limit would be exceeded.");
            if (formats is null) { formats = new XElement(N("numFmts")); Root.AddFirst(formats); }
            formatId = nextFormat;
            formats.Add(new XElement(N("numFmt"), new XAttribute("numFmtId", formatId), new XAttribute("formatCode", code)));
            formats.SetAttributeValue("count", formats.Elements().Count());
        }
        styles.Add(new XElement(N("xf"), new XAttribute("numFmtId", formatId), new XAttribute("fontId", 0),
            new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("xfId", 0), new XAttribute("applyNumberFormat", 1)));
        styles.SetAttributeValue("count", index + 1);
        Modified = true;
        return selected[slot] = index;
    }

    private static bool Known(string name) => name is "styleSheet" or "numFmts" or "numFmt" or "fonts" or "font" or "name" or
        "charset" or "family" or "b" or "i" or "strike" or "outline" or "shadow" or "condense" or "extend" or "color" or "sz" or
        "u" or "vertAlign" or "scheme" or "fills" or "fill" or "patternFill" or "fgColor" or "bgColor" or "gradientFill" or "stop" or
        "borders" or "border" or "start" or "end" or "left" or "right" or "top" or "bottom" or "diagonal" or "vertical" or "horizontal" or
        "cellStyleXfs" or "xf" or "alignment" or "protection" or "cellXfs" or "cellStyles" or "cellStyle" or "dxfs" or "dxf" or
        "tableStyles" or "tableStyle" or "tableStyleElement" or "colors" or "indexedColors" or "rgbColor" or "mruColors";

    private static bool KnownAttribute(string name) => name is "count" or "val" or "numFmtId" or "formatCode" or "fontId" or "fillId" or "borderId" or "xfId" or
        "applyNumberFormat" or "applyFont" or "applyFill" or "applyBorder" or "applyAlignment" or "applyProtection" or "pivotButton" or "quotePrefix" or
        "auto" or "indexed" or "rgb" or "theme" or "tint" or "patternType" or "type" or "degree" or "left" or "right" or "top" or "bottom" or "position" or
        "diagonalUp" or "diagonalDown" or "outline" or "style" or "horizontal" or "vertical" or "textRotation" or "wrapText" or "shrinkToFit" or
        "indent" or "relativeIndent" or "justifyLastLine" or "readingOrder" or "mergeCell" or "locked" or "hidden" or "name" or "builtinId" or "iLevel" or
        "customBuiltin" or "dxfId" or "table" or "pivot" or "defaultTableStyle" or "defaultPivotStyle" or "size" or "knownFonts";
}
