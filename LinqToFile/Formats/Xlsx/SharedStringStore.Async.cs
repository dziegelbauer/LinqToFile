using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Xml;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

internal sealed partial class SharedStringStore
{
    private byte[]? asyncBytes;
    private byte[]? asyncEntry;
    internal async ValueTask LoadAsync(XmlReader reader, string ns, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await reader.MoveToContentAsync().ConfigureAwait(false);
        if (reader.LocalName != "sst" || reader.NamespaceURI != ns) throw new XlsxFormatException("The shared-string root or namespace is invalid.");
        XlsxXml.CheckRequiredMarkup(reader);
        var text = new XlsxText(ns, options);
        var row = new TabularRow(1024, null);
        bool ended = reader.IsEmptyElement;
        while (await XlsxXml.ReadAsync(reader, token).ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0) { ended = true; continue; }
            if (reader.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(reader); continue; }
            XlsxXml.CheckRequiredMarkup(reader);
            if (reader.Depth != 1 || reader.NamespaceURI != ns) throw new XlsxFormatException("The shared-string table contains invalid markup.");
            if (reader.LocalName == "extLst") { await XlsxText.SkipAsync(reader, token).ConfigureAwait(false); continue; }
            if (reader.LocalName != "si") throw new XlsxFormatException("The shared-string table contains invalid markup.");
            row.Reset();
            await text.ReadRichAsync(reader, row, token).ConfigureAwait(false);
            await AddAsync(row, token).ConfigureAwait(false);
        }
        if (!ended) throw new XlsxFormatException("The shared-string table is incomplete.");
        // Finish buffered writes asynchronously before later random-access reads seek these files.
        if (data is not null)
        {
            await data.FlushAsync(token).ConfigureAwait(false);
            await index!.FlushAsync(token).ConfigureAwait(false);
        }
    }

    private async ValueTask AddAsync(TabularRow row, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (TryAddMemory(row.CurrentField)) return;
        if (data is null)
        {
            data = resources.CreateTemporary();
            index = resources.CreateTemporary();
            if (values is not null)
                foreach (string existing in values)
                    await WriteAsync(existing.AsMemory(), token).ConfigureAwait(false);
            values = null;
            textBytes = 0;
        }
        await WriteAsync(row.CurrentFieldMemory, token).ConfigureAwait(false);
        count++;
    }

    private async ValueTask WriteAsync(ReadOnlyMemory<char> value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        resources.ReserveTemporary(12L + value.Length * 2L);
        asyncEntry ??= new byte[12];
        asyncBytes ??= new byte[2048];
        BinaryPrimitives.WriteInt64LittleEndian(asyncEntry, data!.Length);
        BinaryPrimitives.WriteInt32LittleEndian(asyncEntry.AsSpan(8), value.Length);
        await index!.WriteAsync(asyncEntry, token).ConfigureAwait(false);
        while (!value.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            int characters = Math.Min(value.Length, asyncBytes.Length / 2);
            MemoryMarshal.AsBytes(value.Span[..characters]).CopyTo(asyncBytes);
            await data.WriteAsync(asyncBytes.AsMemory(0, characters * 2), token).ConfigureAwait(false);
            value = value[characters..];
        }
    }

    internal async ValueTask<string?> CopyToAsync(long ordinal, TabularRow target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (ordinal < 0 || ordinal >= count) throw new XlsxFormatException("A shared-string index is out of range.");
        if (values is not null)
        {
            string retained = values[(int)ordinal];
            target.Append(retained);
            return retained;
        }
        asyncEntry ??= new byte[12];
        asyncBytes ??= new byte[2048];
        index!.Position = checked(ordinal * 12);
        await index.ReadExactlyAsync(asyncEntry, token).ConfigureAwait(false);
        data!.Position = BinaryPrimitives.ReadInt64LittleEndian(asyncEntry);
        int length = BinaryPrimitives.ReadInt32LittleEndian(asyncEntry.AsSpan(8));
        while (length > 0)
        {
            token.ThrowIfCancellationRequested();
            int read = Math.Min(length, chunk.Length);
            await data.ReadExactlyAsync(asyncBytes.AsMemory(0, read * 2), token).ConfigureAwait(false);
            asyncBytes.AsSpan(0, read * 2).CopyTo(MemoryMarshal.AsBytes(chunk.AsSpan(0, read)));
            target.Append(chunk.AsSpan(0, read));
            length -= read;
        }
        return null;
    }
}
