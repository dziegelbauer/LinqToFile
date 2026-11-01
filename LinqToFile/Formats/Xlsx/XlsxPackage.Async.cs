using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace LinqToFile.Formats.Xlsx;

internal sealed partial class XlsxPackage
{
    private XlsxPackage(ZipArchive archive, XlsxResources resources)
    {
        this.archive = archive;
        Resources = resources;
    }

    internal static async ValueTask<XlsxPackage> CreateAsync(Stream source, XlsxOptions options,
        XlsxResources resources, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var view = new ArchiveViewStream(source, token);
        if (options.MaxWorkbookBytes is long maximum && view.Length > maximum)
            throw Error("The workbook-byte limit was exceeded.");
        var archive = await ZipArchive.CreateAsync(view, ZipArchiveMode.Read, false, null, token).ConfigureAwait(false);
        var package = new XlsxPackage(archive, resources);
        try { await package.InitializeAsync(options, token).ConfigureAwait(false); return package; }
        catch { await package.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private async ValueTask InitializeAsync(XlsxOptions options, CancellationToken token)
    {
            // .NET 10 lazily reads the central directory synchronously through Entries.
            // Part opening and compressed part reads below use the async APIs.
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/')) continue;
                if (entry.FullName.StartsWith('/') || entry.FullName.Split('/').Any(s => s is "" or "." or ".."))
                    throw Error("An archive part name is invalid.");
                string name = Resolve("", entry.FullName);
                if (!parts.TryAdd(name, entry)) throw Error("Duplicate package parts are ambiguous.");
            }
            var types = await LoadAsync("[Content_Types].xml", "Types", XlsxXml.ContentTypes, token).ConfigureAwait(false);
            foreach (var item in types.Elements())
            {
                token.ThrowIfCancellationRequested();
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
            var rootRelationships = await RelationshipsAsync("", token).ConfigureAwait(false);
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
            var workbook = await LoadAsync(workbookPart, "workbook", SpreadsheetNamespace, token).ConfigureAwait(false);
            var relationships = await RelationshipsAsync(workbookPart, token).ConfigureAwait(false);
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
                token.ThrowIfCancellationRequested();
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
                    using var xml = await OpenPartAsync(part, token).ConfigureAwait(false);
                    SharedStrings = new(options, Resources);
                    await SharedStrings.LoadAsync(xml, SpreadsheetNamespace, token).ConfigureAwait(false);
                }
                catch (TabularFileException error) { error.PartName ??= part; error.WorksheetName ??= WorksheetName; throw; }
                catch (XmlException) { throw Error("The shared-string XML is malformed.", part); }
                catch (InvalidDataException) { throw Error("The shared-string part could not be decompressed.", part); }
            }
    }

    internal ValueTask<XmlReader> OpenWorksheetAsync(CancellationToken token) => OpenPartAsync(WorksheetPart, token);

    private async ValueTask<XmlReader> OpenPartAsync(string part, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var stream = Resources.Count(await RequirePart(part).OpenAsync(token).ConfigureAwait(false), part);
        try { return XlsxXml.Open(stream, async: true); }
        catch { await stream.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private async ValueTask<XElement> LoadAsync(string part, string rootName, string ns, CancellationToken token)
    {
        try
        {
            using var reader = await OpenPartAsync(part, token).ConfigureAwait(false);
            // Enforce must-understand semantics on every materialized metadata element.
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, token).ConfigureAwait(false);
            var root = document.Root;
            if (root?.Name != XName.Get(rootName, ns)) throw Error("A required XML root or namespace is invalid.", part);
            foreach (var element in root.DescendantsAndSelf())
            {
                token.ThrowIfCancellationRequested();
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

    private async ValueTask<Dictionary<string, Relationship>> RelationshipsAsync(string source, CancellationToken token)
    {
        int slash = source.LastIndexOf('/');
        string part = source.Length == 0 ? "_rels/.rels" : source[..(slash + 1)] + "_rels/" + source[(slash + 1)..] + ".rels";
        RequireContentType(part, "application/vnd.openxmlformats-package.relationships+xml");
        var root = await LoadAsync(part, "Relationships", XlsxXml.PackageRelationships, token).ConfigureAwait(false);
        var result = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        foreach (var item in root.Elements())
        {
            token.ThrowIfCancellationRequested();
            if (item.Name != XName.Get("Relationship", XlsxXml.PackageRelationships)) throw Error("A relationship declaration is invalid.", part);
            string? mode = (string?)item.Attribute("TargetMode");
            if (mode is not (null or "Internal" or "External")) throw Error("A relationship target mode is invalid.", part);
            if (!result.TryAdd(Required(item, "Id", part), new(Required(item, "Type", part), Required(item, "Target", part), mode == "External")))
                throw Error("Duplicate relationship identifiers are ambiguous.", part);
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        try { await archive.DisposeAsync().ConfigureAwait(false); }
        finally { await Resources.DisposeAsync().ConfigureAwait(false); }
    }
}
