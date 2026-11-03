using System.Xml;

namespace LinqToFile.Formats.Xlsx;

internal static class XlsxXml
{
    internal const string Transitional = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal const string Strict = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    internal const string TransitionalRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    internal const string StrictRelationships = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    internal const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    internal const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    internal const string Compatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    internal static XmlReader Open(Stream stream, bool async = false) => XmlReader.Create(stream, new XmlReaderSettings
    {
        Async = async,
        CloseInput = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true
    });

    internal static async ValueTask<bool> ReadAsync(XmlReader reader, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        bool result = await reader.ReadAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return result;
    }

    internal static void CheckRequiredMarkup(XmlReader reader)
    {
        string? required = reader.GetAttribute("MustUnderstand", Compatibility);
        if (required is not null)
            foreach (string prefix in required.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                string? ns = reader.LookupNamespace(prefix);
                if (ns is not (Transitional or Strict or TransitionalRelationships or StrictRelationships or PackageRelationships or ContentTypes))
                    throw new XlsxFormatException("Required XML markup is not supported.");
            }
    }

    internal static void CheckNonElement(XmlReader reader)
    {
        if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA && !string.IsNullOrWhiteSpace(reader.Value))
            throw new XlsxFormatException("Unexpected text occurs outside a cell value.");
    }
}
