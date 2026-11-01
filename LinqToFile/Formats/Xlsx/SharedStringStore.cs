using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Xml;
using LinqToFile.Infrastructure;

namespace LinqToFile.Formats.Xlsx;

// Memory table until its budget is reached; then both content and fixed-width index live on disk.
internal sealed partial class SharedStringStore(XlsxOptions options, XlsxResources resources)
{
    private List<string>? values;
    private FileStream? data;
    private FileStream? index;
    private long textBytes;
    private long count;
    private readonly char[] chunk = new char[1024];
    internal long Count => count;
    internal bool Spilled => data is not null;
    internal long AccountedMemoryBytes => values is null ? 0 : textBytes + 32L + 24L + (long)values.Capacity * IntPtr.Size;

    internal void Load(XmlReader reader, string ns)
    {
        reader.MoveToContent();
        if (reader.LocalName != "sst" || reader.NamespaceURI != ns) throw new XlsxFormatException("The shared-string root or namespace is invalid.");
        XlsxXml.CheckRequiredMarkup(reader);
        var text = new XlsxText(ns, options);
        var row = new TabularRow(1024, null);
        bool ended = reader.IsEmptyElement;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0) { ended = true; continue; }
            if (reader.NodeType != XmlNodeType.Element) { XlsxXml.CheckNonElement(reader); continue; }
            XlsxXml.CheckRequiredMarkup(reader);
            if (reader.Depth != 1 || reader.NamespaceURI != ns) throw new XlsxFormatException("The shared-string table contains invalid markup.");
            if (reader.LocalName == "extLst") { XlsxText.Skip(reader); continue; }
            if (reader.LocalName != "si") throw new XlsxFormatException("The shared-string table contains invalid markup.");
            row.Reset();
            text.ReadRich(reader, row);
            Add(row.CurrentField);
        }
        if (!ended) throw new XlsxFormatException("The shared-string table is incomplete.");
    }

    private bool TryAddMemory(ReadOnlySpan<char> value)
    {
        if (data is not null) return false;
        int capacity = values?.Capacity ?? 0;
        if (values is null || values.Count == capacity) capacity = capacity == 0 ? 4 : checked(capacity * 2);
        long cost = (22L + value.Length * 2L + 7) & ~7L;
        if (textBytes + cost + 32L + 24L + (long)capacity * IntPtr.Size <= options.SharedStringMemoryBudget)
        {
            values ??= [];
            values.EnsureCapacity(capacity);
            values.Add(value.ToString());
            textBytes += cost;
            count++;
            return true;
        }
        return false;
    }

    private void Add(ReadOnlySpan<char> value)
    {
        if (TryAddMemory(value)) return;
        if (data is null)
        {
            data = resources.CreateTemporary();
            index = resources.CreateTemporary();
            if (values is not null)
                foreach (string existing in values) Write(existing);
            values = null;
            textBytes = 0;
        }
        Write(value);
        count++;
    }

    private void Write(ReadOnlySpan<char> value)
    {
        resources.ReserveTemporary(12L + value.Length * 2L);
        Span<byte> entry = stackalloc byte[12];
        BinaryPrimitives.WriteInt64LittleEndian(entry, data!.Length);
        BinaryPrimitives.WriteInt32LittleEndian(entry[8..], value.Length);
        index!.Write(entry);
        data.Write(MemoryMarshal.AsBytes(value));
    }

    internal string? CopyTo(long ordinal, TabularRow target)
    {
        if (ordinal < 0 || ordinal >= count) throw new XlsxFormatException("A shared-string index is out of range.");
        if (values is not null)
        {
            string retained = values[(int)ordinal];
            target.Append(retained);
            return retained;
        }
        Span<byte> entry = stackalloc byte[12];
        index!.Position = checked(ordinal * 12);
        index.ReadExactly(entry);
        long offset = BinaryPrimitives.ReadInt64LittleEndian(entry);
        int length = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
        data!.Position = offset;
        while (length > 0)
        {
            int read = Math.Min(length, chunk.Length);
            data.ReadExactly(MemoryMarshal.AsBytes(chunk.AsSpan(0, read)));
            target.Append(chunk.AsSpan(0, read));
            length -= read;
        }
        return null;
    }
}
