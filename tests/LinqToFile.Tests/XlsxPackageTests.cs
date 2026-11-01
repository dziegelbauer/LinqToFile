using System.IO.Compression;
using System.Text;
using LinqToFile.Tests.Support;
using Xunit;
using static LinqToFile.Tests.Support.XlsxFixture;

namespace LinqToFile.Tests;

public sealed class XlsxPackageTests
{
    [Theory]
    [InlineData("[Content_Types].xml")]
    [InlineData("_rels/.rels")]
    [InlineData("xl/workbook.xml")]
    [InlineData("xl/_rels/workbook.xml.rels")]
    [InlineData("xl/worksheets/data.xml")]
    public void MissingRequiredPartsFailWithoutLeakingParserDetails(string part)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts.Remove(part));
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("_rels/.rels")]
    [InlineData("xl/workbook.xml")]
    [InlineData("xl/_rels/workbook.xml.rels")]
    [InlineData("xl/worksheets/data.xml")]
    public void MalformedXmlIsSanitized(string part)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts[part] += "<secret-sensitive-data>");
        var error = Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
        Assert.DoesNotContain("secret-sensitive-data", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(part, error.PartName);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("xl/workbook.xml")]
    [InlineData("xl/worksheets/data.xml")]
    public void DtdsAndExternalEntitiesAreProhibited(string part)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts[part] = "<!DOCTYPE x [<!ENTITY sensitive SYSTEM 'file:///not-read'>]>" + parts[part]);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Fact]
    public void RejectsDuplicateArchivePartsIncludingCaseAliases()
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts["XL/WORKBOOK.XML"] = parts["xl/workbook.xml"]);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Theory]
    [InlineData("application/vnd.ms-excel.sheet.macroEnabled.main+xml")]
    [InlineData("application/vnd.ms-excel.sheet.binary.macroEnabled.main")]
    public void IdentifiesUnsupportedFormatsByContentTypeRatherThanExtension(string contentType)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts["[Content_Types].xml"] = parts["[Content_Types].xml"]
            .Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", contentType, StringComparison.Ordinal));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Theory]
    [InlineData("_rels/.rels")]
    [InlineData("xl/_rels/workbook.xml.rels")]
    public void RejectsExternalRequiredRelationships(string part)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts[part] = parts[part].Replace("Target=", "TargetMode=\"External\" Target=", StringComparison.Ordinal));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Fact]
    public void ResolvesRootRelativeTargetsAndCustomWorkbookLocations()
    {
        using var stream = Create(SingleColumn("42"), change: parts =>
        {
            parts["[Content_Types].xml"] = parts["[Content_Types].xml"].Replace("/xl/workbook.xml", "/books/main.xml", StringComparison.Ordinal);
            parts["_rels/.rels"] = parts["_rels/.rels"].Replace("xl/workbook.xml", "/books/main.xml", StringComparison.Ordinal);
            parts["books/main.xml"] = parts["xl/workbook.xml"];
            parts.Remove("xl/workbook.xml");
            parts["books/_rels/main.xml.rels"] = parts["xl/_rels/workbook.xml.rels"].Replace("worksheets/data.xml", "../xl/worksheets/data.xml", StringComparison.Ordinal);
            parts.Remove("xl/_rels/workbook.xml.rels");
        });
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).Single().Value);
    }

    [Theory]
    [InlineData("../../../outside.xml")]
    [InlineData("https://example.invalid/sheet.xml")]
    [InlineData("worksheets/%2fdata.xml")]
    [InlineData("worksheets/%ZZ.xml")]
    public void RejectsInvalidRelationshipTargets(string target)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts["xl/_rels/workbook.xml.rels"] = parts["xl/_rels/workbook.xml.rels"]
            .Replace("worksheets/data.xml", target, StringComparison.Ordinal));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Theory]
    [InlineData("_rels/.rels", "Relationship")]
    [InlineData("xl/_rels/workbook.xml.rels", "Relationship")]
    [InlineData("xl/workbook.xml", "sheet")]
    public void DuplicateRelationshipsAndSheetNamesAreAmbiguous(string part, string element)
    {
        using var stream = Create(SingleColumn("42"), change: parts =>
        {
            int start = parts[part].IndexOf("<" + element + " ", StringComparison.Ordinal);
            int end = parts[part].IndexOf("/>", start, StringComparison.Ordinal) + 2;
            parts[part] = parts[part].Insert(end, parts[part][start..end]);
        });
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Fact]
    public void NamedSelectionIsIndependentOfOrderAndIgnoresUnselectedBadCells()
    {
        using var stream = Create(SingleColumn("42"), change: parts =>
        {
            parts["[Content_Types].xml"] = parts["[Content_Types].xml"].Replace("</Types>", "<Override PartName=\"/xl/worksheets/other.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>", StringComparison.Ordinal);
            parts["xl/workbook.xml"] = parts["xl/workbook.xml"].Replace("</sheets>", "<sheet name=\" Other \" sheetId=\"2\" r:id=\"other\" state=\"veryHidden\"/></sheets>", StringComparison.Ordinal);
            parts["xl/_rels/workbook.xml.rels"] = parts["xl/_rels/workbook.xml.rels"].Replace("</Relationships>", "<Relationship Id=\"other\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/other.xml\"/></Relationships>", StringComparison.Ordinal);
            parts["xl/worksheets/other.xml"] = parts["xl/worksheets/data.xml"];
            parts["xl/worksheets/data.xml"] = parts["xl/worksheets/data.xml"].Replace("<v>42</v>", "<v>not-a-number</v>", StringComparison.Ordinal);
        });
        Assert.Equal(42, TabularFile.Xlsx<SheetInteger>(stream).WithWorksheet(" other ").Single().Value);
    }

    [Fact]
    public void MissingNamedSheetDoesNotFallBackToDefault()
    {
        using var stream = Create(SingleColumn("42"));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).WithWorksheet("Other").Single());
    }

    [Theory]
    [InlineData("xl/workbook.xml", "workbook")]
    [InlineData("xl/worksheets/data.xml", "worksheet")]
    public void UnsupportedMustUnderstandMarkupFails(string part, string root)
    {
        using var stream = Create(SingleColumn("42"), change: parts => parts[part] = parts[part].Replace("<" + root + " ",
            "<" + root + " xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:unknown=\"urn:unknown\" mc:MustUnderstand=\"unknown\" ", StringComparison.Ordinal));
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(stream).Single());
    }

    [Fact]
    public void NonSeekableAndNonZipSourcesFailClearlyAndRespectOwnership()
    {
        var probe = new ProbeStream([1, 2, 3]);
        Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(probe, leaveOpen: false).ToArray());
        Assert.Equal(1, probe.SyncDisposals);
        using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("not an XLSX archive"));
        Assert.Null(Assert.Throws<XlsxFormatException>(() => TabularFile.Xlsx<SheetInteger>(invalid).Single()).InnerException);
    }
}
