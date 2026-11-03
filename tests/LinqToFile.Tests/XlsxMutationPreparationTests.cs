using System.Text;
using System.Xml;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class XlsxMutationPreparationTests
{
    [Theory]
    [InlineData(false, XlsxXml.Transitional)]
    [InlineData(true, XlsxXml.Transitional)]
    [InlineData(false, XlsxXml.Strict)]
    [InlineData(true, XlsxXml.Strict)]
    public async Task ScansValuesWithoutConvertingThemOrRetainingRows(bool asynchronous, string ns)
    {
        var xml = "<worksheet xmlns=\"" + ns + "\"><sheetViews><sheetView workbookViewId=\"0\"><selection activeCell=\"A1\" sqref=\"A1\"/></sheetView></sheetViews>" +
            "<sheetData><row r=\"3\"><c t=\"inlineStr\"><is><t>not-a-number</t></is></c></row></sheetData>" +
            "<mergeCells><mergeCell ref=\"A1:D2\"/></mergeCells></worksheet>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = XlsxXml.Open(stream, asynchronous);
        var scan = new XlsxMutationScan(ns, XlsxMutationXmlKind.Worksheet, true, "xl/worksheets/sheet1.xml");
        if (asynchronous) await scan.ReadAsync(reader, CancellationToken.None); else scan.Read(reader);
        Assert.Equal(2, scan.LastMergedRow);
        Assert.True(scan.HasViewCoordinates);
    }

    [Theory]
    [InlineData("<sheetData><row><c><f>PRIVATE_FORMULA</f><v>1</v></c></row></sheetData>")]
    [InlineData("<tableParts/>")]
    [InlineData("<drawing/>")]
    [InlineData("<hyperlinks/>")]
    [InlineData("<dataValidations/>")]
    [InlineData("<conditionalFormatting/>")]
    [InlineData("<extLst/>")]
    [InlineData("<foreign xmlns=\"urn:unknown\"/>")]
    public async Task UnsupportedFeaturesThrowForSelectedAndUnselectedSheetsWithoutLeakingContent(string content)
    {
        foreach (bool selected in new[] { false, true })
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<worksheet xmlns=\"" + XlsxXml.Transitional + "\">" + content + "</worksheet>"));
            using var reader = XlsxXml.Open(stream, true);
            var scan = new XlsxMutationScan(XlsxXml.Transitional, XlsxMutationXmlKind.Worksheet, selected, "sheet.xml");
            var error = await Assert.ThrowsAsync<XlsxFormatException>(() => scan.ReadAsync(reader, CancellationToken.None).AsTask());
            Assert.DoesNotContain("PRIVATE_FORMULA", error.ToString(), StringComparison.Ordinal);
            Assert.Null(error.InnerException);
            Assert.Equal("sheet.xml", error.PartName);
        }
    }

    [Fact]
    public void ProtectionAndNamedRangesAreRejectedWithTheApprovedScope()
    {
        Assert.Throws<XlsxFormatException>(() => Scan("workbook", "<workbookProtection/>", XlsxMutationXmlKind.Workbook, false));
        Assert.Throws<XlsxFormatException>(() => Scan("workbook", "<definedNames/>", XlsxMutationXmlKind.Workbook, false));
        Assert.Throws<XlsxFormatException>(() => Scan("worksheet", "<sheetProtection/>", XlsxMutationXmlKind.Worksheet, true));
        Scan("worksheet", "<sheetProtection/>", XlsxMutationXmlKind.Worksheet, false);
        Assert.Throws<XlsxFormatException>(() => Scan("worksheet", "<mergeCells><mergeCell ref=\"D2:A1\"/></mergeCells>", XlsxMutationXmlKind.Worksheet, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedXmlDoesNotExposeEntityNames(bool asynchronous)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<worksheet xmlns=\"" + XlsxXml.Transitional + "\"><sheetData><row><c><v>&PRIVATE_SECRET;</v></c></row></sheetData></worksheet>"));
        using var reader = XlsxXml.Open(stream, asynchronous);
        var scan = new XlsxMutationScan(XlsxXml.Transitional, XlsxMutationXmlKind.Worksheet, true, "sheet.xml");
        var error = asynchronous
            ? await Assert.ThrowsAsync<XlsxFormatException>(() => scan.ReadAsync(reader, CancellationToken.None).AsTask())
            : Assert.Throws<XlsxFormatException>(() => scan.Read(reader));
        Assert.DoesNotContain("PRIVATE_SECRET", error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.Equal("sheet.xml", error.PartName);
    }

    private static void Scan(string root, string content, XlsxMutationXmlKind kind, bool selected)
    {
        using var text = new StringReader("<" + root + " xmlns=\"" + XlsxXml.Transitional + "\">" + content + "</" + root + ">");
        using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        new XlsxMutationScan(XlsxXml.Transitional, kind, selected, "part.xml").Read(reader);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TemporaryLimitsAreSharedAndRejectedBeforeDestinationGrowth(bool asynchronous)
    {
        using var resources = new XlsxResources(new XlsxOptions { MaxTemporaryBytes = 5 });
        using var spool = new MemoryStream();
        using var target = new MemoryStream();
        using var first = new XlsxMutationOutputStream(spool, resources);
        using var second = new XlsxMutationOutputStream(target, resources, maximumBytes: 3);
        if (asynchronous) { await first.WriteAsync(new byte[3]); await second.WriteAsync(new byte[2]); }
        else { first.Write(new byte[3]); second.Write(new byte[2]); }
        if (asynchronous) await Assert.ThrowsAsync<XlsxFormatException>(() => second.WriteAsync(new byte[1]).AsTask());
        else Assert.Throws<XlsxFormatException>(() => second.Write(new byte[1]));
        Assert.Equal(5, resources.TemporaryBytes);
        Assert.Equal(2, target.Length);
    }

    [Fact]
    public async Task OutputLimitIncludesFinalizationWritesAndCancellationAvoidsReservation()
    {
        using var resources = new XlsxResources(new XlsxOptions());
        using var target = new ExportProbeStream(asyncOnly: true);
        using var output = new XlsxMutationOutputStream(target, resources, maximumBytes: 3);
        await output.WriteAsync(new byte[3]);
        await Assert.ThrowsAsync<XlsxFormatException>(() => output.WriteAsync(new byte[1]).AsTask());
        Assert.Equal(3, resources.TemporaryBytes);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => output.WriteAsync(new byte[1], cancellation.Token).AsTask());
        Assert.Equal(3, resources.TemporaryBytes);
        Assert.Equal(1, target.AsyncWrites);
        Assert.Equal(0, target.SyncWrites);
    }
}
