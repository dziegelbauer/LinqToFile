using System.Globalization;
using System.Xml;
using LinqToFile.Formats.Xlsx;
using LinqToFile.Reading;
using LinqToFile.Sources;

namespace LinqToFile;

/// <summary>An immutable destination for complete, single-worksheet XLSX exports.</summary>
/// <remarks>A supplied stream permits one export across all derived configurations. Factory streams are disposed after each export. Stream exports can leave partial output on failure.</remarks>
/// <typeparam name="T">The mapped record type.</typeparam>
public sealed class XlsxWriter<T> where T : class, ITabularRecord<T>
{
    private readonly string? path;
    private readonly StreamDestination? destination;
    private readonly XlsxWriteOptions options;
    internal XlsxWriter(string? path, StreamDestination? destination, XlsxWriteOptions options)
    { this.path = path; this.destination = destination; this.options = options; }

    private XlsxWriter<T> Configure(XlsxWriteOptions value) => new(path, destination, value);

    /// <summary>Sets the name of the exported worksheet. The default is Sheet1.</summary>
    /// <param name="name">A valid Excel worksheet name of at most 31 characters.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
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

    /// <summary>Sets the date system for a new workbook. The default is Excel1900.</summary>
    /// <param name="value">Excel1900 or Excel1904. None is not supported.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public XlsxWriter<T> WithDateSystem(TabularDateSystem value)
    {
        if (value is not (TabularDateSystem.Excel1900 or TabularDateSystem.Excel1904))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { DateSystem = value });
    }

    /// <summary>Sets whether complete-document creation can replace an existing path. The default is Throw.</summary>
    /// <param name="value">The publication behavior. This option does not change append or insertion.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public XlsxWriter<T> WithExistingFileBehavior(ExistingFileBehavior value)
    {
        if (path is null) throw new InvalidOperationException("Existing-file behavior applies only to path destinations.");
        if (value is not (ExistingFileBehavior.Throw or ExistingFileBehavior.Overwrite))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Configure(options with { ExistingFileBehavior = value });
    }

    /// <summary>Uses a read-only culture snapshot for text conversion. Native XLSX numbers use invariant culture.</summary>
    /// <param name="value">The conversion culture. The default is invariant culture.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public XlsxWriter<T> WithCulture(CultureInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = CultureInfo.ReadOnly((CultureInfo)value.Clone());
        T.Mapper.ValidateCulture(culture);
        return Configure(options with { Culture = culture });
    }

    /// <summary>Sets the comparison used to match column names.</summary>
    /// <param name="value">StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase. The default is ordinal.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The option value is not supported.</exception>
    public XlsxWriter<T> WithHeaderComparison(StringComparer value)
    {
        if (!ReferenceEquals(value, StringComparer.Ordinal) && !ReferenceEquals(value, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase.", nameof(value));
        SchemaBinding<T>.ValidateMapping(value);
        return Configure(options with { HeaderComparison = value });
    }
    /// <summary>Controls whitespace trimming for field conversion and output validation. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim field whitespace for conversion and validation.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public XlsxWriter<T> WithFieldTrimming(bool enabled = true) => Configure(options with { TrimFields = enabled });
    /// <summary>Controls whitespace trimming for header matching. Disabled by default.</summary>
    /// <param name="enabled">Whether to trim header whitespace.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    public XlsxWriter<T> WithHeaderTrimming(bool enabled = true) => Configure(options with { TrimHeaders = enabled });
    /// <summary>Limits worksheet cell text length.</summary>
    /// <param name="length">A nonnegative number of UTF-16 code units, or null for no configured limit.</param>
    /// <returns>A new configuration with this option. The current instance is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The option is outside its supported range.</exception>
    public XlsxWriter<T> WithMaxCellTextLength(int? length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        return Configure(options with { MaxCellTextLength = length });
    }

    /// <summary>Writes a complete XLSX document with headers only.</summary>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write() => XlsxExport<T>.Write(path, destination, options, []);
    /// <summary>Writes a complete XLSX document with headers and one data record.</summary>
    /// <param name="record">The record to write.</param>
    /// <returns>One on success.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write(T record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return XlsxExport<T>.Write(path, destination, options, [record]);
    }
    /// <summary>Writes a complete XLSX document with headers and the supplied data records.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure.</remarks>
    public long Write(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.Write(path, destination, options, records);
    }
    /// <summary>Writes a complete XLSX document with headers only asynchronously.</summary>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>Zero, because headers do not count as data records.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(CancellationToken cancellationToken = default) =>
        XlsxExport<T>.WriteAsync(path, destination, options, [], null, cancellationToken);
    /// <summary>Writes a complete XLSX document with headers and one data record asynchronously.</summary>
    /// <param name="record">The record to write.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>One on success.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return XlsxExport<T>.WriteAsync(path, destination, options, [record], null, cancellationToken);
    }
    /// <summary>Writes a complete XLSX document with headers and the supplied data records asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.WriteAsync(path, destination, options, records, null, cancellationToken);
    }
    /// <summary>Writes a complete XLSX document with headers and the supplied data records asynchronously.</summary>
    /// <param name="records">The records to write, consumed incrementally.</param>
    /// <param name="cancellationToken">The token used to request cancellation.</param>
    /// <returns>The number of data records written, excluding headers; zero for an empty operation.</returns>
    /// <remarks>A supplied stream permits one export across all derived configurations. Stream output can be partial on failure. Stream exports observe cancellation during output. Staged path exports observe cancellation until publication begins.</remarks>
    public ValueTask<long> WriteAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        return XlsxExport<T>.WriteAsync(path, destination, options, null, records, cancellationToken);
    }
}
