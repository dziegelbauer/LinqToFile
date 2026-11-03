using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace LinqToFile.Formats.Xlsx;

// Only small package/workbook metadata is materialized. Worksheet cell XML is streamed.
internal sealed partial class XlsxPackage : IDisposable, IAsyncDisposable
{
    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> parts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase);
    internal string WorksheetName { get; private set; } = null!;
    internal string WorksheetPart { get; private set; } = null!;
    internal string SpreadsheetNamespace { get; private set; } = null!;
    internal bool Date1904 { get; private set; }
    internal SharedStringStore? SharedStrings { get; private set; }
    internal XlsxResources Resources { get; private set; }

    internal XlsxPackage(Stream source, XlsxOptions options, XlsxResources? resources = null)
    {
        var view = new ArchiveViewStream(source);
        if (options.MaxWorkbookBytes is long maximum && view.Length > maximum)
            throw Error("The workbook-byte limit was exceeded.");
        Resources = resources ?? new(options);
        archive = new ZipArchive(view, ZipArchiveMode.Read, leaveOpen: false);
        try
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                if (entry.FullName.StartsWith('/') || entry.FullName.Split('/').Any(s => s is "" or "." or ".."))
                    throw Error("An archive part name is invalid.");
                string name = Resolve("", entry.FullName);
                if (!parts.TryAdd(name, entry)) throw Error("Duplicate package parts are ambiguous.");
            }
            var types = Load("[Content_Types].xml", "Types", XlsxXml.ContentTypes);
            foreach (var item in types.Elements())
            {
                if (item.Name.NamespaceName != XlsxXml.ContentTypes) throw Error("Invalid content-type markup.", "[Content_Types].xml");
                string contentType = Required(item, "ContentType", "[Content_Types].xml");
                bool added = item.Name.LocalName switch
                {
                    "Override" => overrides.TryAdd(Resolve("", Required(item, "PartName", "[Content_Types].xml")), contentType),
                    "Default" => defaults.TryAdd(Required(item, "Extension", "[Content_Types].xml"), contentType),
                    _ => throw Error("Invalid content-type declaration.", "[Content_Types].xml")
                };
                if (!added) throw Error("Duplicate content-type declarations are ambiguous.", "[Content_Types].xml");
            }
            var rootRelationships = Relationships("");
            var office = rootRelationships.Values.Where(r => r.Type == XlsxXml.TransitionalRelationships + "/officeDocument" ||
                r.Type == XlsxXml.StrictRelationships + "/officeDocument").ToArray();
            if (office.Length != 1) throw Error("Exactly one workbook relationship is required.", "_rels/.rels");
            var main = office[0];
            if (main.External) throw Error("An external workbook relationship is not supported.", "_rels/.rels");
            string workbookPart = Resolve("", main.Target);
            RequireContentType(workbookPart, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            SpreadsheetNamespace = main.Type.StartsWith(XlsxXml.StrictRelationships, StringComparison.Ordinal)
                ? XlsxXml.Strict : XlsxXml.Transitional;
            string relationNamespace = SpreadsheetNamespace == XlsxXml.Strict ? XlsxXml.StrictRelationships : XlsxXml.TransitionalRelationships;
            var workbook = Load(workbookPart, "workbook", SpreadsheetNamespace);
            var relationships = Relationships(workbookPart);
            var properties = workbook.Elements(XName.Get("workbookPr", SpreadsheetNamespace)).ToArray();
            if (properties.Length > 1) throw Error("Duplicate workbook properties are invalid.", workbookPart);
            string? dateSystem = properties.Length == 0 ? null : (string?)properties[0].Attribute("date1904");
            Date1904 = dateSystem switch { null or "0" or "false" => false, "1" or "true" => true,
                _ => throw Error("The workbook date system is invalid.", workbookPart) };
            var sheets = workbook.Elements(XName.Get("sheets", SpreadsheetNamespace)).ToArray();
            if (sheets.Length != 1) throw Error("A workbook must have one sheet collection.", workbookPart);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<uint>();
            var links = new HashSet<string>(StringComparer.Ordinal);
            string? selectedName = null;
            string? selectedPart = null;
            foreach (var sheet in sheets[0].Elements())
            {
                if (sheet.Name != XName.Get("sheet", SpreadsheetNamespace)) throw Error("Invalid sheet declaration.", workbookPart);
                string name = XlsxText.DecodeMetadata(Required(sheet, "name", workbookPart));
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) throw Error("Worksheet names are invalid or ambiguous.", workbookPart);
                if (!uint.TryParse(Required(sheet, "sheetId", workbookPart), NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 || !ids.Add(id))
                    throw Error("Sheet identifiers are invalid or ambiguous.", workbookPart);
                string link = (string?)sheet.Attribute(XName.Get("id", relationNamespace)) ?? throw Error("A sheet relationship is missing.", workbookPart);
                if (!links.Add(link) || !relationships.TryGetValue(link, out var relation)) throw Error("A sheet relationship is missing or ambiguous.", workbookPart);
                bool requested = options.Worksheet is not null && StringComparer.OrdinalIgnoreCase.Equals(name, options.Worksheet);
                if (relation.Type != relationNamespace + "/worksheet")
                {
                    if (requested) throw Error("The requested sheet is not a worksheet.", workbookPart);
                    continue;
                }
                if (selectedName is not null || (options.Worksheet is not null && !requested)) continue;
                if (relation.External) throw Error("An external worksheet relationship is not supported.", workbookPart);
                selectedPart = Resolve(workbookPart, relation.Target);
                RequireContentType(selectedPart, "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                RequirePart(selectedPart);
                selectedName = name;
            }
            WorksheetName = selectedName ?? throw Error("The requested worksheet could not be found.", workbookPart);
            WorksheetPart = selectedPart!;
            var stringRelations = relationships.Values.Where(r => r.Type == relationNamespace + "/sharedStrings").ToArray();
            if (stringRelations.Length > 1) throw Error("Shared-string relationships are ambiguous.", workbookPart);
            if (stringRelations.Length == 1)
            {
                var relation = stringRelations[0];
                if (relation.External) throw Error("An external shared-string relationship is not supported.", workbookPart);
                string part = Resolve(workbookPart, relation.Target);
                RequireContentType(part, "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml");
                try
                {
                    using var xml = OpenPart(part);
                    SharedStrings = new(options, Resources);
                    SharedStrings.Load(xml, SpreadsheetNamespace);
                }
                catch (TabularFileException error) { error.PartName ??= part; error.WorksheetName ??= WorksheetName; throw; }
                catch (XmlException) { throw Error("The shared-string XML is malformed.", part); }
                catch (InvalidDataException) { throw Error("The shared-string part could not be decompressed.", part); }
            }
        }
        catch { Dispose(); throw; }
    }

    internal XmlReader OpenWorksheet() => OpenPart(WorksheetPart);
    private XmlReader OpenPart(string part) => XlsxXml.Open(Resources.Count(RequirePart(part).Open(), part));

    private XElement Load(string part, string rootName, string ns)
    {
        try
        {
            using var reader = OpenPart(part);
            // Enforce must-understand semantics on every materialized metadata element.
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root?.Name != XName.Get(rootName, ns)) throw Error("A required XML root or namespace is invalid.", part);
            foreach (var element in root.DescendantsAndSelf())
            {
                var required = (string?)element.Attribute(XName.Get("MustUnderstand", XlsxXml.Compatibility));
                if (required is null) continue;
                foreach (string prefix in required.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    string? requiredNamespace = element.GetNamespaceOfPrefix(prefix)?.NamespaceName;
                    if (requiredNamespace != ns && requiredNamespace != XlsxXml.TransitionalRelationships && requiredNamespace != XlsxXml.StrictRelationships)
                        throw Error("Required XML markup is not supported.", part);
                }
            }
            return root;
        }
        catch (XmlException) { throw Error("A package XML part is malformed.", part); }
        catch (InvalidDataException) { throw Error("A package part could not be decompressed.", part); }
    }

    private Dictionary<string, Relationship> Relationships(string source)
    {
        int slash = source.LastIndexOf('/');
        string part = source.Length == 0 ? "_rels/.rels" : source[..(slash + 1)] + "_rels/" + source[(slash + 1)..] + ".rels";
        RequireContentType(part, "application/vnd.openxmlformats-package.relationships+xml");
        var root = Load(part, "Relationships", XlsxXml.PackageRelationships);
        var result = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        foreach (var item in root.Elements())
        {
            if (item.Name != XName.Get("Relationship", XlsxXml.PackageRelationships)) throw Error("A relationship declaration is invalid.", part);
            string? mode = (string?)item.Attribute("TargetMode");
            if (mode is not (null or "Internal" or "External")) throw Error("A relationship target mode is invalid.", part);
            if (!result.TryAdd(Required(item, "Id", part), new(Required(item, "Type", part), Required(item, "Target", part), mode == "External")))
                throw Error("Duplicate relationship identifiers are ambiguous.", part);
        }
        return result;
    }

    private void RequireContentType(string part, string expected)
    {
        string? type = overrides.GetValueOrDefault(part);
        if (type is null)
        {
            int dot = part.LastIndexOf('.');
            if (dot >= 0) type = defaults.GetValueOrDefault(part[(dot + 1)..]);
        }
        if (type != expected) throw Error("A required part has an unsupported content type; only XLSX workbooks are supported.", part);
    }

    private ZipArchiveEntry RequirePart(string part) => parts.TryGetValue(part, out var entry)
        ? entry : throw Error("A required package part is missing.", part);

    private static string Required(XElement element, string name, string part) =>
        (string?)element.Attribute(name) is string value && value.Length != 0 ? value : throw Error("A required metadata attribute is missing.", part);

    internal static string Resolve(string source, string target)
    {
        if (target.Length == 0 || target.IndexOfAny(['\\', '?', '#', ':']) >= 0)
            throw Error("A package part URI is invalid.");
        for (int i = 0; i < target.Length; i++)
            if (target[i] == '%' && (i + 2 >= target.Length || !char.IsAsciiHexDigit(target[i + 1]) || !char.IsAsciiHexDigit(target[i + 2])))
                throw Error("A package part URI escape is invalid.");
        string decoded = Uri.UnescapeDataString(target);
        if (decoded.IndexOfAny(['\\', '?', '#', ':', '\0']) >= 0 || target.Contains("%2f", StringComparison.OrdinalIgnoreCase))
            throw Error("A package part URI is invalid.");
        var segments = new List<string>();
        if (!target.StartsWith('/') && source.LastIndexOf('/') is int last && last >= 0)
            segments.AddRange(source[..last].Split('/'));
        foreach (string segment in decoded.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) throw Error("A package relationship escapes the archive root.");
                segments.RemoveAt(segments.Count - 1);
            }
            else segments.Add(segment);
        }
        if (segments.Count == 0) throw Error("A package part URI is invalid.");
        return string.Join('/', segments);
    }

    internal static XlsxFormatException Error(string message, string? part = null) => new(message) { PartName = part };
    public void Dispose() { try { archive.Dispose(); } finally { Resources.Dispose(); } }
    private sealed record Relationship(string Type, string Target, bool External);
}
