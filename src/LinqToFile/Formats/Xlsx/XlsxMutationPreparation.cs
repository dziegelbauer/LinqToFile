using LinqToFile.Reading;
using System.Xml;

namespace LinqToFile.Formats.Xlsx;

internal sealed class XlsxMutationPreparation<T> : IDisposable, IAsyncDisposable where T : class, ITabularRecord<T>
{
    internal XlsxPackage Package { get; }
    internal XlsxMutationMetadata Metadata { get; }
    internal XlsxOutput<T> Output { get; }
    internal int InsertionRow { get; }
    internal int LastRow { get; }
    internal int MaxColumn { get; }
    private readonly XlsxRecordReader reader;

    private XlsxMutationPreparation(XlsxPackage package, XlsxMutationMetadata metadata, XlsxRecordReader reader,
        XlsxOptions options, int[] order, int insertionRow)
    {
        Package = package; Metadata = metadata; this.reader = reader;
        LastRow = reader.LastPhysicalRow;
        InsertionRow = insertionRow < 0 ? LastRow + 1 : insertionRow;
        MaxColumn = Math.Max(reader.Row.Headers.Length, metadata.SelectedScan.MaxColumn);
        Output = new(new XlsxWriteOptions
        {
            Worksheet = package.WorksheetName, Culture = options.Culture, TrimFields = options.TrimFields,
            DateSystem = package.Date1904 ? TabularDateSystem.Excel1904 : TabularDateSystem.Excel1900,
            MaxCellTextLength = options.MaxCellTextLength
        }, new(order, reader.Row.Headers, package.WorksheetPart, package.SpreadsheetNamespace, metadata.Styles));
    }

    internal static XlsxMutationPreparation<T> Open(Stream stream, XlsxOptions options, long? position)
    {
        XlsxPackage package;
        try { package = XlsxInput.Open(stream, options); }
        catch (InvalidDataException) { throw new XlsxFormatException("The workbook is not a supported XLSX archive."); }
        XlsxRecordReader? reader = null;
        try
        {
            var metadata = package.PrepareMutation();
            reader = new XlsxRecordReader(package, options);
            if (!reader.Read()) throw MissingHeader(package);
            var order = Bind(reader, options, metadata);
            long count = 0;
            int insertion = -1;
            while (reader.Read())
            {
                if (position == count) insertion = checked((int)reader.Row.WorksheetRowNumber!.Value);
                count++;
            }
            if (position > count) throw new ArgumentOutOfRangeException(nameof(position));
            return new(package, metadata, reader, options, order, insertion);
        }
        catch (Exception error)
        {
            try { if (reader is null) package.Dispose(); else reader.Dispose(); }
            catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            if (error is XmlException or InvalidDataException) throw XlsxPackage.Error("A workbook part is malformed or could not be decompressed.");
            throw;
        }
    }

    internal static async ValueTask<XlsxMutationPreparation<T>> OpenAsync(Stream stream, XlsxOptions options, long? position, CancellationToken token)
    {
        XlsxPackage package;
        try { package = await XlsxInput.OpenAsync(stream, options, token).ConfigureAwait(false); }
        catch (InvalidDataException) { throw new XlsxFormatException("The workbook is not a supported XLSX archive."); }
        XlsxRecordReader? reader = null;
        try
        {
            var metadata = await package.PrepareMutationAsync(token).ConfigureAwait(false);
            reader = new XlsxRecordReader(package, options, false);
            await reader.InitializeAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw MissingHeader(package);
            var order = Bind(reader, options, metadata);
            long count = 0;
            int insertion = -1;
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (position == count) insertion = checked((int)reader.Row.WorksheetRowNumber!.Value);
                count++;
            }
            if (position > count) throw new ArgumentOutOfRangeException(nameof(position));
            return new(package, metadata, reader, options, order, insertion);
        }
        catch (Exception error)
        {
            try
            {
                if (reader is null) await package.DisposeAsync().ConfigureAwait(false);
                else await reader.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
            if (error is XmlException or InvalidDataException) throw XlsxPackage.Error("A workbook part is malformed or could not be decompressed.");
            throw;
        }
    }

    private static int[] Bind(XlsxRecordReader reader, XlsxOptions options, XlsxMutationMetadata metadata)
    {
        var ordinals = SchemaBinding<T>.Bind(reader.Row, options);
        if (reader.Row.Headers.Length != ordinals.Length)
            throw reader.Row.WithLocation(new TabularSchemaException("Mutation requires an exact model/header match.", 1, 0, 0));
        if (metadata.SelectedScan.LastMergedRow >= reader.Row.WorksheetRowNumber)
            throw reader.Row.WithLocation(new XlsxFormatException("A merged range reaches the selected table rows."));
        var order = new int[ordinals.Length];
        for (int i = 0; i < ordinals.Length; i++) order[ordinals[i]] = i;
        return order;
    }

    private static TabularSchemaException MissingHeader(XlsxPackage package) => new("The selected worksheet has no header.", 1, 0, 0)
        { WorksheetName = package.WorksheetName, PartName = package.WorksheetPart };
    public void Dispose() => reader.Dispose();
    public ValueTask DisposeAsync() => reader.DisposeAsync();
}
