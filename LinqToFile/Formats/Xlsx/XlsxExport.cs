using System.IO.Compression;
using System.Text;
using LinqToFile.Sources;
using LinqToFile.Writing;
using static LinqToFile.Formats.Xlsx.XlsxDocument;

namespace LinqToFile.Formats.Xlsx;

internal static class XlsxExport<T> where T : class, ITabularRecord<T>
{
    internal static long Write(string? path, StreamDestination? destination, XlsxWriteOptions options, IEnumerable<T> rows)
    {
        if (path is not null)
        {
            var output = new XlsxOutput<T>(options);
            using var staged = new StagedDocument(path, options.ExistingFileBehavior);
            long count;
            using (var stream = staged.Open(false)) count = Archive(stream, output, options, rows);
            staged.Publish(CancellationToken.None);
            return count;
        }
        StreamLease? lease = destination!.Begin(false);
        bool complete = false;
        try
        {
            var output = new XlsxOutput<T>(options);
            lease ??= destination.Open();
            long count = Archive(lease.Value.Stream, output, options, rows);
            complete = true;
            return count;
        }
        finally
        {
            if (lease is { } acquired)
            {
                if (complete) acquired.Dispose();
                else try { acquired.Dispose(); } catch (Exception error) when (error is not OutOfMemoryException) { }
            }
        }
    }

    internal static async ValueTask<long> WriteAsync(string? path, StreamDestination? destination, XlsxWriteOptions options,
        IEnumerable<T>? rows, IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        if (path is not null)
        {
            token.ThrowIfCancellationRequested();
            var output = new XlsxOutput<T>(options);
            using var staged = new StagedDocument(path, options.ExistingFileBehavior);
            long count;
            await using (var stream = staged.Open(true))
                count = await ArchiveAsync(stream, output, options, rows, asyncRows, token).ConfigureAwait(false);
            staged.Publish(token);
            return count;
        }
        StreamLease? lease = destination!.Begin(true);
        bool complete = false;
        long result;
        try
        {
            token.ThrowIfCancellationRequested();
            var output = new XlsxOutput<T>(options);
            token.ThrowIfCancellationRequested();
            lease ??= await destination.OpenAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            result = await ArchiveAsync(lease.Value.Stream, output, options, rows, asyncRows, token).ConfigureAwait(false);
            complete = true;
        }
        finally
        {
            if (lease is { } acquired)
            {
                if (complete) await acquired.DisposeAsync().ConfigureAwait(false);
                else try { await acquired.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { }
            }
        }
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static long Archive(Stream stream, XlsxOutput<T> output, XlsxWriteOptions options, IEnumerable<T> rows)
    {
        RequireWritable(stream);
        using var view = new ForwardWriteStream(stream, CancellationToken.None);
        var archive = new ZipArchive(view, ZipArchiveMode.Create, true);
        long count = 0;
        Stream? active = null;
        try
        {
            foreach (var (name, text) in Parts(options))
            {
                active = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                active.Write(Utf8.GetBytes(text));
                active.Dispose(); active = null;
            }
            active = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open();
            {
                var encoded = new EncodedOutput(active, Utf8);
                encoded.Write(SheetStart.AsMemory(), 0);
                encoded.Write(output.Record, 0);
                foreach (var row in rows)
                {
                    output.Row(row, checked(count + 1));
                    encoded.Write(output.Record, count + 1);
                    count++;
                }
                encoded.Write(SheetEnd.AsMemory(), count, true);
            }
            active.Dispose(); active = null;
        }
        catch
        {
            try { active?.Dispose(); } catch (Exception error) when (error is not OutOfMemoryException) { }
            try { archive.Dispose(); } catch (Exception error) when (error is not OutOfMemoryException) { }
            throw;
        }
        archive.Dispose();
        stream.Flush();
        return count;
    }

    private static async ValueTask<long> ArchiveAsync(Stream stream, XlsxOutput<T> output, XlsxWriteOptions options,
        IEnumerable<T>? rows, IAsyncEnumerable<T>? asyncRows, CancellationToken token)
    {
        RequireWritable(stream);
        using var view = new ForwardWriteStream(stream, token, true);
        var archive = await ZipArchive.CreateAsync(view, ZipArchiveMode.Create, true, null, token).ConfigureAwait(false);
        long count = 0;
        Stream? active = null;
        try
        {
            foreach (var (name, text) in Parts(options))
            {
                token.ThrowIfCancellationRequested();
                active = await archive.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(token).ConfigureAwait(false);
                await active.WriteAsync(Utf8.GetBytes(text), token).ConfigureAwait(false);
                await active.DisposeAsync().ConfigureAwait(false); active = null;
                await view.DrainAsync().ConfigureAwait(false);
            }
            active = await archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).OpenAsync(token).ConfigureAwait(false);
            {
                var encoded = new EncodedOutput(active, Utf8);
                await encoded.WriteAsync(SheetStart.AsMemory(), 0, token).ConfigureAwait(false);
                await encoded.WriteAsync(output.Record, 0, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await using var input = new WriteInput<T>(rows, asyncRows, token);
                while (await input.MoveNextAsync(token).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    output.Row(input.Current, checked(count + 1));
                    await encoded.WriteAsync(output.Record, count + 1, token).ConfigureAwait(false);
                    await view.DrainAsync().ConfigureAwait(false);
                    count++;
                }
                await encoded.WriteAsync(SheetEnd.AsMemory(), count, token, true).ConfigureAwait(false);
            }
            await active.DisposeAsync().ConfigureAwait(false); active = null;
            token.ThrowIfCancellationRequested();
        }
        catch
        {
            try { if (active is not null) await active.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) when (error is not OutOfMemoryException) { }
            try { await archive.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) when (error is not OutOfMemoryException) { }
            throw;
        }
        await archive.DisposeAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await view.FlushAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return count;
    }

    private static void RequireWritable(Stream stream)
    {
        if (!stream.CanWrite) throw new InvalidOperationException("The destination stream must support writing.");
    }

}

internal static class XlsxDocument
{
    internal static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    internal const string SheetStart = "<?xml version=\"1.0\" encoding=\"utf-8\"?><worksheet xmlns=\"" + XlsxXml.Transitional + "\"><sheetData>";
    internal const string SheetEnd = "</sheetData></worksheet>";

    internal static IEnumerable<(string Name, string Text)> Parts(XlsxWriteOptions options)
    {
        yield return ("[Content_Types].xml", "<Types xmlns=\"" + XlsxXml.ContentTypes + "\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
        yield return ("_rels/.rels", "<Relationships xmlns=\"" + XlsxXml.PackageRelationships + "\"><Relationship Id=\"rId1\" Type=\"" + XlsxXml.TransitionalRelationships + "/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        string name = System.Security.SecurityElement.Escape(options.Worksheet);
        yield return ("xl/workbook.xml", "<workbook xmlns=\"" + XlsxXml.Transitional + "\" xmlns:r=\"" + XlsxXml.TransitionalRelationships + "\"><workbookPr date1904=\"" + (options.DateSystem == TabularDateSystem.Excel1904 ? "1" : "0") + "\"/><sheets><sheet name=\"" + name + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        yield return ("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"" + XlsxXml.PackageRelationships + "\"><Relationship Id=\"rId1\" Type=\"" + XlsxXml.TransitionalRelationships + "/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"" + XlsxXml.TransitionalRelationships + "/styles\" Target=\"styles.xml\"/></Relationships>");
        yield return ("xl/styles.xml", "<styleSheet xmlns=\"" + XlsxXml.Transitional + "\"><numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd\"/><numFmt numFmtId=\"165\" formatCode=\"yyyy-mm-dd hh:mm:ss.000\"/><numFmt numFmtId=\"166\" formatCode=\"hh:mm:ss.000\"/></numFmts><fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
    }
}
