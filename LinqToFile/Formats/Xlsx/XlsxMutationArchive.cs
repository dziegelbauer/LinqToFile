using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using LinqToFile.Writing;

namespace LinqToFile.Formats.Xlsx;

internal static class XlsxMutationArchive
{
    internal static void Copy(XlsxPackage package, XlsxMutationMetadata metadata, Stream target, Stream spool,
        int insertionRow, long count, int lastRow, int maxColumn)
    {
        var replacements = Replacements(package, metadata);
        using var view = new ForwardWriteStream(target, CancellationToken.None);
        var archive = new ZipArchive(view, ZipArchiveMode.Create, true);
        Stream? active = null;
        try
        {
            foreach (var original in package.MutationEntries)
            {
                var entry = archive.CreateEntry(original.FullName, CompressionLevel.Optimal);
                entry.LastWriteTime = original.LastWriteTime; entry.ExternalAttributes = original.ExternalAttributes;
                active = entry.Open();
                if (!original.FullName.EndsWith('/'))
                {
                    string part = XlsxPackage.Resolve("", original.FullName);
                    if (part.Equals(package.WorksheetPart, StringComparison.OrdinalIgnoreCase))
                    {
                        using var xml = package.OpenWorksheet();
                        using var writer = XmlWriter.Create(active, Settings(false));
                        new XlsxMutationWorksheet(insertionRow, count, lastRow, maxColumn, spool).Copy(xml, writer);
                    }
                    else if (replacements.Remove(part, out var replacement))
                    {
                        using var writer = XmlWriter.Create(active, Settings(false));
                        replacement.WriteTo(writer);
                    }
                    else
                    {
                        using var input = package.MutationOpen(part);
                        input.CopyTo(active, 64 * 1024);
                    }
                }
                active.Dispose(); active = null;
            }
            foreach (var (part, replacement) in replacements)
            {
                active = archive.CreateEntry(part, CompressionLevel.Optimal).Open();
                using (var writer = XmlWriter.Create(active, Settings(false))) replacement.WriteTo(writer);
                active.Dispose(); active = null;
            }
        }
        catch (Exception error)
        {
            try { active?.Dispose(); } catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            try { archive.Dispose(); } catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            if (error is XmlException or InvalidDataException) throw XlsxPackage.Error("A workbook part is malformed or could not be decompressed.");
            throw;
        }
        archive.Dispose(); target.Flush();
    }

    internal static async ValueTask CopyAsync(XlsxPackage package, XlsxMutationMetadata metadata, Stream target, Stream spool,
        int insertionRow, long count, int lastRow, int maxColumn, CancellationToken token)
    {
        var replacements = Replacements(package, metadata);
        using var view = new ForwardWriteStream(target, token, true);
        var archive = await ZipArchive.CreateAsync(view, ZipArchiveMode.Create, true, null, token).ConfigureAwait(false);
        Stream? active = null;
        try
        {
            foreach (var original in package.MutationEntries)
            {
                token.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry(original.FullName, CompressionLevel.Optimal);
                entry.LastWriteTime = original.LastWriteTime; entry.ExternalAttributes = original.ExternalAttributes;
                active = await entry.OpenAsync(token).ConfigureAwait(false);
                if (!original.FullName.EndsWith('/'))
                {
                    string part = XlsxPackage.Resolve("", original.FullName);
                    if (part.Equals(package.WorksheetPart, StringComparison.OrdinalIgnoreCase))
                    {
                        using var xml = await package.OpenWorksheetAsync(token).ConfigureAwait(false);
                        await using var writer = XmlWriter.Create(active, Settings(true));
                        await new XlsxMutationWorksheet(insertionRow, count, lastRow, maxColumn, spool, view.DrainAsync).CopyAsync(xml, writer, token).ConfigureAwait(false);
                    }
                    else if (replacements.Remove(part, out var replacement))
                    {
                        await using var writer = XmlWriter.Create(active, Settings(true));
                        await replacement.WriteToAsync(writer, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await using var input = await package.MutationOpenAsync(part, token).ConfigureAwait(false);
                        await input.CopyToAsync(active, 64 * 1024, token).ConfigureAwait(false);
                    }
                }
                await active.DisposeAsync().ConfigureAwait(false); active = null;
                await view.DrainAsync().ConfigureAwait(false);
            }
            foreach (var (part, replacement) in replacements)
            {
                active = await archive.CreateEntry(part, CompressionLevel.Optimal).OpenAsync(token).ConfigureAwait(false);
                await using (var writer = XmlWriter.Create(active, Settings(true)))
                    await replacement.WriteToAsync(writer, token).ConfigureAwait(false);
                await active.DisposeAsync().ConfigureAwait(false); active = null;
                await view.DrainAsync().ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error)
        {
            try { if (active is not null) await active.DisposeAsync().ConfigureAwait(false); } catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            try { await archive.DisposeAsync().ConfigureAwait(false); } catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            if (error is XmlException or InvalidDataException) throw XlsxPackage.Error("A workbook part is malformed or could not be decompressed.");
            throw;
        }
        await archive.DisposeAsync().ConfigureAwait(false);
        await view.FlushAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
    }

    private static XmlWriterSettings Settings(bool asynchronous) => new()
    { Async = asynchronous, Encoding = XlsxDocument.Utf8, CloseOutput = false, CheckCharacters = true, NamespaceHandling = NamespaceHandling.OmitDuplicates };

    private static Dictionary<string, XElement> Replacements(XlsxPackage package, XlsxMutationMetadata metadata)
    {
        var result = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        if (!metadata.Styles.Modified) return result;
        string? part = metadata.StylesPart;
        if (part is null)
        {
            string workbookName = package.MutationEntries.Single(e => !e.FullName.EndsWith('/') &&
                XlsxPackage.Resolve("", e.FullName).Equals(metadata.WorkbookPart, StringComparison.OrdinalIgnoreCase)).FullName;
            string folder = workbookName[..(workbookName.LastIndexOf('/') + 1)];
            var existing = new HashSet<string>(package.MutationEntries.Select(e => XlsxPackage.Resolve("", e.FullName)), StringComparer.OrdinalIgnoreCase);
            int suffix = 0;
            string name;
            do { name = "styles-linqtofile" + (suffix == 0 ? "" : suffix.ToString(CultureInfo.InvariantCulture)) + ".xml"; suffix++; } while (existing.Contains(XlsxPackage.Resolve("", folder + name)));
            part = folder + name;
            string relationship = package.SpreadsheetNamespace == XlsxXml.Strict ? XlsxXml.StrictRelationships : XlsxXml.TransitionalRelationships;
            var ids = new HashSet<string>(metadata.Relations.Elements().Select(e => e.Attribute("Id")!.Value), StringComparer.Ordinal);
            string id = "rIdLinqToFileStyles";
            for (int i = 1; ids.Contains(id); i++) id = "rIdLinqToFileStyles" + i.ToString(CultureInfo.InvariantCulture);
            metadata.Relations.Add(new XElement(XName.Get("Relationship", XlsxXml.PackageRelationships), new XAttribute("Id", id),
                new XAttribute("Type", relationship + "/styles"), new XAttribute("Target", name)));
            metadata.Types.Add(new XElement(XName.Get("Override", XlsxXml.ContentTypes), new XAttribute("PartName", "/" + part),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));
            result.Add(metadata.RelationsPart, metadata.Relations);
            result.Add("[Content_Types].xml", metadata.Types);
        }
        result.Add(part, metadata.Styles.Root!);
        return result;
    }
}
