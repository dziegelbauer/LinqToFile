using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace LinqToFile.Formats.Xlsx;

internal sealed record XlsxMutationMetadata(string WorkbookPart, string RelationsPart, XElement Types,
    XElement Relations, string? StylesPart, XlsxMutationStyles Styles, XlsxMutationScan SelectedScan);

internal sealed partial class XlsxPackage
{
    private const string WorkbookType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string WorksheetType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private const string StylesType = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";
    private const string SharedType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml";
    private const string RelationshipType = "application/vnd.openxmlformats-package.relationships+xml";

    internal IReadOnlyList<ZipArchiveEntry> MutationEntries => archive.Entries;
    internal Stream MutationOpen(string part) => Resources.Count(RequirePart(part).Open(), part);
    internal async ValueTask<Stream> MutationOpenAsync(string part, CancellationToken token) =>
        Resources.Count(await RequirePart(part).OpenAsync(token).ConfigureAwait(false), part);

    internal XlsxMutationMetadata PrepareMutation()
    {
        ValidateMutationParts();
        string workbookPart = parts.Single(p => ContentType(p.Key) == WorkbookType).Key;
        var workbook = Load(workbookPart, "workbook", SpreadsheetNamespace);
        using (var xml = workbook.CreateReader()) new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Workbook, false, workbookPart).Read(xml);
        XElement? relations = null;
        foreach (var part in parts.Keys.Where(p => ContentType(p) == RelationshipType))
        {
            var root = Load(part, "Relationships", XlsxXml.PackageRelationships);
            CheckMutationRelations(part, root);
            if (part.Equals(RelationPart(workbookPart), StringComparison.OrdinalIgnoreCase)) relations = root;
        }
        string? stylePart = FindStyles(workbookPart, relations!);
        var styles = new XlsxMutationStyles(stylePart is null ? null : Load(stylePart, "styleSheet", SpreadsheetNamespace), SpreadsheetNamespace);
        var selectedScan = new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Worksheet, true, WorksheetPart) { StyleCount = styles.OriginalCount };
        foreach (var part in parts.Keys.Where(p => ContentType(p) == WorksheetType))
        {
            using var xml = OpenPart(part);
            var scan = part.Equals(WorksheetPart, StringComparison.OrdinalIgnoreCase) ? selectedScan : new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Worksheet, false, part);
            scan.Read(xml);
        }
        ValidateMutationMetadata();
        foreach (var part in parts.Keys.Where(p => ContentType(p) == SharedType))
        {
            using var xml = OpenPart(part);
            new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.SharedStrings, false, part).Read(xml);
        }
        return new(workbookPart, RelationPart(workbookPart), Load("[Content_Types].xml", "Types", XlsxXml.ContentTypes), relations!, stylePart, styles, selectedScan);
    }

    internal async ValueTask<XlsxMutationMetadata> PrepareMutationAsync(CancellationToken token)
    {
        ValidateMutationParts();
        string workbookPart = parts.Single(p => ContentType(p.Key) == WorkbookType).Key;
        var workbook = await LoadAsync(workbookPart, "workbook", SpreadsheetNamespace, token).ConfigureAwait(false);
        using (var xml = workbook.CreateReader()) new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Workbook, false, workbookPart).Read(xml);
        XElement? relations = null;
        foreach (var part in parts.Keys.Where(p => ContentType(p) == RelationshipType))
        {
            var root = await LoadAsync(part, "Relationships", XlsxXml.PackageRelationships, token).ConfigureAwait(false);
            CheckMutationRelations(part, root);
            if (part.Equals(RelationPart(workbookPart), StringComparison.OrdinalIgnoreCase)) relations = root;
        }
        string? stylePart = FindStyles(workbookPart, relations!);
        var styles = new XlsxMutationStyles(stylePart is null ? null : await LoadAsync(stylePart, "styleSheet", SpreadsheetNamespace, token).ConfigureAwait(false), SpreadsheetNamespace);
        var selectedScan = new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Worksheet, true, WorksheetPart) { StyleCount = styles.OriginalCount };
        foreach (var part in parts.Keys.Where(p => ContentType(p) == WorksheetType))
        {
            using var xml = await OpenPartAsync(part, token).ConfigureAwait(false);
            var scan = part.Equals(WorksheetPart, StringComparison.OrdinalIgnoreCase) ? selectedScan : new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.Worksheet, false, part);
            await scan.ReadAsync(xml, token).ConfigureAwait(false);
        }
        foreach (var part in parts.Keys.Where(IsAdditionalMetadata))
        {
            using var xml = await OpenPartAsync(part, token).ConfigureAwait(false);
            while (await XlsxXml.ReadAsync(xml, token).ConfigureAwait(false)) CheckMetadataElement(xml, part);
        }
        foreach (var part in parts.Keys.Where(p => ContentType(p) == SharedType))
        {
            using var xml = await OpenPartAsync(part, token).ConfigureAwait(false);
            await new XlsxMutationScan(SpreadsheetNamespace, XlsxMutationXmlKind.SharedStrings, false, part).ReadAsync(xml, token).ConfigureAwait(false);
        }
        return new(workbookPart, RelationPart(workbookPart), await LoadAsync("[Content_Types].xml", "Types", XlsxXml.ContentTypes, token).ConfigureAwait(false), relations!, stylePart, styles, selectedScan);
    }

    private string? ContentType(string part)
    {
        if (overrides.TryGetValue(part, out var value)) return value;
        int dot = part.LastIndexOf('.');
        return dot < 0 ? null : defaults.GetValueOrDefault(part[(dot + 1)..]);
    }

    private void ValidateMutationParts()
    {
        if (parts.Count(p => ContentType(p.Key) == WorkbookType) != 1) throw Error("Mutation requires exactly one workbook part.");
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                if (entry.FullName.StartsWith('/') || entry.FullName.Split('/').Any(s => s is "." or "..")) throw Error("An archive directory is invalid.");
                Resolve("", entry.FullName.TrimEnd('/'));
                continue;
            }
            string part = Resolve("", entry.FullName);
            if (part.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) continue;
            if (ContentType(part) is not (WorkbookType or WorksheetType or StylesType or SharedType or RelationshipType or
                "application/vnd.openxmlformats-officedocument.theme+xml" or "application/vnd.openxmlformats-package.core-properties+xml" or
                "application/vnd.openxmlformats-officedocument.extended-properties+xml" or "application/vnd.openxmlformats-officedocument.custom-properties+xml"))
                throw Error("A package part is not supported for workbook mutation.", part);
        }
    }

    private static string RelationPart(string source)
    {
        int slash = source.LastIndexOf('/');
        return source[..(slash + 1)] + "_rels/" + source[(slash + 1)..] + ".rels";
    }

    private void CheckMutationRelations(string part, XElement root)
    {
        string source;
        if (part.Equals("_rels/.rels", StringComparison.OrdinalIgnoreCase)) source = "";
        else
        {
            int marker = part.LastIndexOf("/_rels/", StringComparison.OrdinalIgnoreCase);
            source = marker >= 0 ? part[..(marker + 1)] + part[(marker + 7)..^5] :
                part.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase) ? part[6..^5] : throw Error("A relationship part path is invalid.", part);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relation in root.Elements())
        {
            if (relation.Name != XName.Get("Relationship", XlsxXml.PackageRelationships) ||
                !ids.Add(Required(relation, "Id", part)) || (string?)relation.Attribute("TargetMode") is not (null or "Internal"))
                throw Error("A relationship is invalid or external.", part);
            if (relation.Attributes().Any(a => !a.IsNamespaceDeclaration && (a.Name.NamespaceName.Length != 0 ||
                a.Name.LocalName is not ("Id" or "Type" or "Target" or "TargetMode"))))
                throw Error("A relationship contains unsupported attributes.", part);
            string type = Required(relation, "Type", part);
            string target = Resolve(source, Required(relation, "Target", part));
            string? expected = type switch
            {
                XlsxXml.TransitionalRelationships + "/officeDocument" or XlsxXml.StrictRelationships + "/officeDocument" => WorkbookType,
                XlsxXml.TransitionalRelationships + "/worksheet" or XlsxXml.StrictRelationships + "/worksheet" => WorksheetType,
                XlsxXml.TransitionalRelationships + "/styles" or XlsxXml.StrictRelationships + "/styles" => StylesType,
                XlsxXml.TransitionalRelationships + "/sharedStrings" or XlsxXml.StrictRelationships + "/sharedStrings" => SharedType,
                XlsxXml.TransitionalRelationships + "/theme" or XlsxXml.StrictRelationships + "/theme" => "application/vnd.openxmlformats-officedocument.theme+xml",
                XlsxXml.PackageRelationships + "/metadata/core-properties" => "application/vnd.openxmlformats-package.core-properties+xml",
                XlsxXml.TransitionalRelationships + "/extended-properties" or XlsxXml.StrictRelationships + "/extended-properties" => "application/vnd.openxmlformats-officedocument.extended-properties+xml",
                XlsxXml.TransitionalRelationships + "/custom-properties" or XlsxXml.StrictRelationships + "/custom-properties" => "application/vnd.openxmlformats-officedocument.custom-properties+xml",
                _ => null
            };
            if (expected is null) throw Error("A relationship type is not supported for workbook mutation.", part);
            if (source.Length == 0 ? expected is not (WorkbookType or "application/vnd.openxmlformats-package.core-properties+xml" or
                "application/vnd.openxmlformats-officedocument.extended-properties+xml" or "application/vnd.openxmlformats-officedocument.custom-properties+xml") :
                ContentType(source) != WorkbookType || expected is not (WorksheetType or StylesType or SharedType or "application/vnd.openxmlformats-officedocument.theme+xml"))
                throw Error("A relationship is not supported at this package location.", part);
            RequirePart(target);
            RequireContentType(target, expected);
        }
    }

    private static string? FindStyles(string workbook, XElement relations)
    {
        var styles = relations.Elements().Where(r => ((string?)r.Attribute("Type")) is
            XlsxXml.TransitionalRelationships + "/styles" or XlsxXml.StrictRelationships + "/styles").ToArray();
        if (styles.Length > 1) throw Error("Style relationships are ambiguous.");
        return styles.Length == 0 ? null : Resolve(workbook, Required(styles[0], "Target", RelationPart(workbook)));
    }

    private bool IsAdditionalMetadata(string part) => ContentType(part) is not (WorkbookType or WorksheetType or StylesType or SharedType or RelationshipType) && !part.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase);
    private void ValidateMutationMetadata()
    {
        foreach (var part in parts.Keys.Where(IsAdditionalMetadata))
        {
            using var xml = OpenPart(part);
            while (xml.Read()) CheckMetadataElement(xml, part);
        }
    }

    private static void CheckMetadataElement(XmlReader xml, string part)
    {
        if (xml.NodeType != XmlNodeType.Element) return;
        string ns = xml.NamespaceURI;
        if (xml.LocalName is "extLst" or "AlternateContent" || ns is not (
            "http://schemas.openxmlformats.org/drawingml/2006/main" or "http://purl.oclc.org/ooxml/drawingml/main" or
            "http://schemas.openxmlformats.org/package/2006/metadata/core-properties" or "http://purl.org/dc/elements/1.1/" or
            "http://purl.org/dc/terms/" or "http://purl.org/dc/dcmitype/" or
            "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" or "http://purl.oclc.org/ooxml/officeDocument/extendedProperties" or
            "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties" or "http://purl.oclc.org/ooxml/officeDocument/customProperties" or
            "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes" or "http://purl.oclc.org/ooxml/officeDocument/docPropsVTypes"))
            throw Error("Package metadata contains unsupported extension content.", part);
    }
}
