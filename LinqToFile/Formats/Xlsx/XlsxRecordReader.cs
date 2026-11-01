using System.Globalization;
using System.IO.Compression;
using System.Xml;
using LinqToFile.Infrastructure;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Xlsx;

internal sealed partial class XlsxRecordReader : IRecordReader
{
    private readonly XlsxPackage package;
    private readonly XlsxOptions options;
    private readonly List<(CellAddress First, CellAddress Last)> merges = [];
    private readonly XlsxText text;
    private XmlReader? reader;
    private int previousRow;
    private int width;
    private int column;
    private bool header = true;
    private bool inData;
    private long recordNumber;
    public TabularRow Row { get; } = new(1024, null);

    internal XlsxRecordReader(XlsxPackage package, XlsxOptions options, bool initialize = true)
    {
        this.package = package;
        this.options = options;
        text = new(package.SpreadsheetNamespace, options);
        Row.DateSystem = package.Date1904 ? NumericDateSystem.Excel1904 : NumericDateSystem.Excel1900;
        Row.WorksheetName = package.WorksheetName;
        Row.PartName = package.WorksheetPart;
        if (!initialize) return;
        try
        {
            Preflight();
            reader = package.OpenWorksheet();
        }
        catch (TabularFileException error) { throw Row.WithLocation(error); }
        catch (XmlException) { throw Error("The worksheet XML is malformed."); }
        catch (InvalidDataException) { throw Error("The worksheet could not be decompressed."); }
    }

    private void Preflight()
    {
        using var scan = package.OpenWorksheet();
        scan.MoveToContent();
        if (scan.LocalName != "worksheet" || scan.NamespaceURI != package.SpreadsheetNamespace)
            throw Error("The worksheet root or namespace is invalid.");
        bool dataFound = false;
        bool mergesFound = false;
        bool inMerges = false;
        while (!scan.EOF)
        {
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
            scan.Read();
        }
        if (!dataFound) throw Error("The worksheet has no sheet data element.");
    }

    public bool Read()
    {
        try { return ReadCore(); }
        catch (TabularFileException error) { throw Row.WithLocation(error); }
        catch (XmlException) { throw Error("The worksheet XML is malformed."); }
        catch (InvalidDataException) { throw Error("The worksheet could not be decompressed."); }
    }

    private bool ReadCore()
    {
        var xml = reader!;
        while (xml.Read())
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
            bool content = ReadRow(xml);
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

    private bool ReadRow(XmlReader xml)
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
            while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
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
                if (beforeHeader) { SkipCell(xml); continue; }
                while (Row.Count < address.Column - 1) Row.EndField();
                bool hasValue = ReadCell(xml, address.Column);
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

    private static void SkipCell(XmlReader xml)
    {
        if (xml.IsEmptyElement) return;
        int depth = xml.Depth;
        while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth)) { }
    }

    private bool ReadCell(XmlReader xml, int ordinal)
    {
        try { return ReadCellCore(xml, ordinal); }
        catch (XlsxFormatException error) when (error.ColumnIndex is null)
        {
            var contextual = Error(error.Message, ordinal);
            contextual.PartName = error.PartName ?? contextual.PartName;
            throw contextual;
        }
    }

    private bool ReadCellCore(XmlReader xml, int ordinal)
    {
        string? type = xml.GetAttribute("t");
        if (type is not (null or "n" or "b" or "inlineStr" or "str" or "e" or "s" or "d"))
            throw Error("The cell type is not supported.", ordinal);
        bool valueFound = false, inlineFound = false, formula = false;
        int depth = xml.Depth;
        if (!xml.IsEmptyElement)
        {
            while (xml.Read() && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(xml); continue; }
                XlsxXml.CheckRequiredMarkup(xml);
                if (xml.Depth != depth + 1 || xml.NamespaceURI != package.SpreadsheetNamespace)
                    throw Error("The cell contains unsupported markup.", ordinal);
                if (xml.LocalName == "f")
                {
                    if (formula || valueFound || inlineFound) throw Error("The cell has conflicting formula elements.", ordinal);
                    formula = true;
                    XlsxText.Skip(xml);
                }
                else if (xml.LocalName == "v")
                {
                    if (valueFound || inlineFound || type == "inlineStr") throw Error("The cell has conflicting value elements.", ordinal);
                    valueFound = true;
                    text.ReadValue(xml, Row, escaped: type == "str", enforceLimit: type != "s");
                }
                else if (xml.LocalName == "is")
                {
                    if (inlineFound || valueFound || type != "inlineStr" || formula) throw Error("The cell has conflicting inline text.", ordinal);
                    inlineFound = true;
                    text.ReadRich(xml, Row);
                }
                else if (xml.LocalName == "extLst") XlsxText.Skip(xml);
                else throw Error("The cell contains unsupported markup.", ordinal);
            }
        }
        long? sharedIndex = ResolveSharedIndex(type, formula, valueFound, ordinal);
        string? retained = sharedIndex is long index ? package.SharedStrings!.CopyTo(index, Row) : null;
        return FinishCell(type, formula, ordinal, retained);
    }

    private long? ResolveSharedIndex(string? type, bool formula, bool valueFound, int ordinal)
    {
        if (formula && (!valueFound || type != "str" && Row.CurrentFieldLength == 0))
            throw Error("A formula has no cached result.", ordinal);
        if (type != "s" || Row.CurrentFieldLength == 0) return null;
        if (!long.TryParse(Row.CurrentField.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long index) || package.SharedStrings is null)
            throw Error("A shared-string reference or table is invalid.", ordinal);
        Row.ReplaceCurrentField([]);
        return index;
    }

    private bool FinishCell(string? type, bool formula, int ordinal, string? retained)
    {
        CellKind kind = CellKind.Text;
        if (Row.CurrentFieldLength != 0)
        {
            switch (type)
            {
                case "d":
                    if (!NativeTemporal.TryIso(Row.CurrentField, out _, validateOnly: true)) throw Error("A native ISO date cell is invalid.", ordinal);
                    kind = CellKind.IsoDate;
                    break;
                case "e": throw Error("A spreadsheet error cell was found.", ordinal);
                case "b":
                    var boolean = Row.CurrentField.Trim();
                    if (!boolean.SequenceEqual("0") && !boolean.SequenceEqual("1")) throw Error("A native boolean cell is invalid.", ordinal);
                    Row.ReplaceCurrentField(boolean.SequenceEqual("1") ? "true" : "false");
                    kind = CellKind.Boolean;
                    break;
                case null or "n":
                    if (!IsNumber(Row.CurrentField.Trim())) throw Error("A native numeric cell is invalid.", ordinal);
                    kind = CellKind.Number;
                    break;
            }
        }
        text.CheckLength(Row.CurrentFieldLength);
        bool content = formula || Row.CurrentFieldLength != 0;
        Row.EndField(kind, retained);
        return content;
    }

    private static bool IsNumber(ReadOnlySpan<char> value)
    {
        int i = 0;
        if (i < value.Length && value[i] is '+' or '-') i++;
        int digits = 0;
        while (i < value.Length && char.IsAsciiDigit(value[i])) { i++; digits++; }
        if (i < value.Length && value[i] == '.')
        {
            i++;
            while (i < value.Length && char.IsAsciiDigit(value[i])) { i++; digits++; }
        }
        if (digits == 0) return false;
        if (i < value.Length && value[i] is 'e' or 'E')
        {
            i++;
            if (i < value.Length && value[i] is '+' or '-') i++;
            int start = i;
            while (i < value.Length && char.IsAsciiDigit(value[i])) i++;
            if (start == i) return false;
        }
        return i == value.Length;
    }

    private XlsxFormatException Error(string message, int? ordinal = null) => Row.WithLocation(new XlsxFormatException(message,
        Row.RecordNumber, ordinal, ordinal is int index && index <= Row.Headers.Length ? Row.Headers[index - 1] : null,
        ordinal is int member && member <= Row.TargetMembers.Length ? Row.TargetMembers[member - 1] : null));
    public TabularFileException CreateFieldCountException(int expected) => Error("The record differs from the header range.");
    public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await ReadCoreAsync(cancellationToken).ConfigureAwait(false); }
        catch (TabularFileException error) { throw Row.WithLocation(error); }
        catch (XmlException) { throw Error("The worksheet XML is malformed."); }
        catch (InvalidDataException) { throw Error("The worksheet could not be decompressed."); }
    }
    public void Dispose() { try { reader?.Dispose(); } finally { package.Dispose(); } }
    public async ValueTask DisposeAsync()
    {
        try { reader?.Dispose(); }
        finally { await package.DisposeAsync().ConfigureAwait(false); }
    }

    internal async ValueTask InitializeAsync(CancellationToken token)
    {
        try
        {
            await PreflightAsync(token).ConfigureAwait(false);
            reader = await package.OpenWorksheetAsync(token).ConfigureAwait(false);
        }
        catch (TabularFileException error) { throw Row.WithLocation(error); }
        catch (XmlException) { throw Error("The worksheet XML is malformed."); }
        catch (InvalidDataException) { throw Error("The worksheet could not be decompressed."); }
    }
}

internal sealed class XlsxRecordReaderFactory : IRecordReaderFactory<XlsxOptions>
{
    internal static readonly XlsxRecordReaderFactory Instance = new();
    public IRecordReader Open(Stream stream, XlsxOptions options)
    {
        XlsxPackage? package = null;
        try
        {
            package = XlsxInput.Open(stream, options);
            return new XlsxRecordReader(package, options);
        }
        catch (InvalidDataException)
        {
            package?.Dispose();
            throw new XlsxFormatException("The workbook is not a supported XLSX archive.");
        }
        catch { package?.Dispose(); throw; }
    }
    public async ValueTask<IRecordReader> OpenAsync(Stream stream, XlsxOptions options, CancellationToken cancellationToken)
    {
        XlsxPackage? package = null;
        try
        {
            package = await XlsxInput.OpenAsync(stream, options, cancellationToken).ConfigureAwait(false);
            var reader = new XlsxRecordReader(package, options, initialize: false);
            await reader.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return reader;
        }
        catch (InvalidDataException)
        {
            if (package is not null) await package.DisposeAsync().ConfigureAwait(false);
            throw new XlsxFormatException("The workbook is not a supported XLSX archive.");
        }
        catch
        {
            if (package is not null) await package.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
