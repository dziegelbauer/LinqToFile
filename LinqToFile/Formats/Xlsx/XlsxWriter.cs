using System.Globalization;
using System.Xml;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable destination for complete, single-worksheet XLSX exports.</summary>
public sealed class XlsxWriter<T> where T : class, ITabularRecord<T>
{
    private readonly string? path;
    private readonly StreamDestination? destination;
    private readonly XlsxWriteOptions options;
    internal XlsxWriter(string? path, StreamDestination? destination, XlsxWriteOptions options)
    { this.path = path; this.destination = destination; this.options = options; }

    private XlsxWriter<T> Configure(XlsxWriteOptions value) => new(path, destination, value);

    public XlsxWriter<T> WithWorksheet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 31 || name.AsSpan().IndexOfAny("[]:*?/\\") >= 0 || name[0] == '\'' || name[^1] == '\'' ||
            name.Any(c => c < ' ') || name.Equals("History", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The worksheet name is invalid.", nameof(name));
        try { XmlConvert.VerifyXmlChars(name); }
        catch (XmlException) { throw new ArgumentException("The worksheet name is invalid.", nameof(name)); }
        return Configure(options with { Worksheet = name });
    }

    public XlsxWriter<T> WithDateSystem(TabularDateSystem value)
    {
        if (value is not (TabularDateSystem.Excel1900 or TabularDateSystem.Excel1904))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { DateSystem = value });
    }

    public XlsxWriter<T> WithExistingFileBehavior(ExistingFileBehavior value)
    {
        if (path is null) throw new InvalidOperationException("Existing-file behavior applies only to path destinations.");
        if (value is not (ExistingFileBehavior.Throw or ExistingFileBehavior.Overwrite))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { ExistingFileBehavior = value });
    }

    public XlsxWriter<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }

    public XlsxWriter<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return Configure(options with { HeaderComparison = value });
    }
    public XlsxWriter<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    public XlsxWriter<T> WithHeaderTrimming(bool enabled = true) => Configure(options with { TrimHeaders = enabled });
    public XlsxWriter<T> WithMaxCellTextLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxCellTextLength = length });
    }

    public long Write() => XlsxExport<T>.Write(path, destination, options, []);
    public long Write(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return XlsxExport<T>.Write(path, destination, options, [record]);
    }
    public long Write(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.Write(path, destination, options, records);
    }
    public ValueTask<long> WriteAsync(CancellationToken cancellationToken = default) =>
        XlsxExport<T>.WriteAsync(path, destination, options, [], null, cancellationToken);
    public ValueTask<long> WriteAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return XlsxExport<T>.WriteAsync(path, destination, options, [record], null, cancellationToken);
    }
    public ValueTask<long> WriteAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.WriteAsync(path, destination, options, records, null, cancellationToken);
    }
    public ValueTask<long> WriteAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.WriteAsync(path, destination, options, null, records, cancellationToken);
    }
}
