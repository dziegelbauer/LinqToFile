using LinqToFile.Formats.Csv;
using LinqToFile.Sources;
using LinqToFile.Formats.Xlsx;

namespace LinqToFile;

/// <summary>Creates typed CSV and XLSX sources and complete-document writers.</summary>
public static class TabularFile
{
    /// <summary>Creates an XLSX complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="path">The file path.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>The path is opened separately for each operation.</remarks>
    public static XlsxWriter<T> XlsxWriter<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, null, new XlsxWriteOptions());
    }
    /// <summary>Creates an XLSX complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="stream">The stream, used from its current position without rewinding.</param>
    /// <param name="leaveOpen">Whether to leave the supplied stream open after the operation. The default is true.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>The supplied stream permits one export across all derived configurations. The library does not rewind it.</remarks>
    public static XlsxWriter<T> XlsxWriter<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("The stream must support writing.", nameof(stream));
        return new(null, new StreamDestination(stream, leaveOpen), new XlsxWriteOptions());
    }
    /// <summary>Creates an XLSX complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream.</remarks>
    public static XlsxWriter<T> XlsxWriter<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(null, new StreamDestination(factory), new XlsxWriteOptions());
    }
    /// <summary>Creates an XLSX complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream. Only asynchronous writes are supported.</remarks>
    public static XlsxWriter<T> XlsxWriterAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(null, new StreamDestination(factory), new XlsxWriteOptions());
    }

    /// <summary>Creates a CSV complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="stream">The stream, used from its current position without rewinding.</param>
    /// <param name="leaveOpen">Whether to leave the supplied stream open after the operation. The default is true.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>The supplied stream permits one export across all derived configurations. The library does not rewind it.</remarks>
    public static CsvWriter<T> CsvWriter<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("The stream must support writing.", nameof(stream));
        return new(new StreamDestination(stream, leaveOpen), new CsvOptions());
    }

    /// <summary>Creates a CSV complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream.</remarks>
    public static CsvWriter<T> CsvWriter<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamDestination(factory), new CsvOptions());
    }

    /// <summary>Creates a CSV complete-document writer.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A complete-document writer. No output is opened until a write operation.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream. Only asynchronous writes are supported.</remarks>
    public static CsvWriter<T> CsvWriterAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamDestination(factory), new CsvOptions());
    }

    /// <summary>Creates an XLSX deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="path">The file path.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>The path is opened separately for each operation.</remarks>
    public static XlsxFile<T> Xlsx<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, new XlsxOptions());
    }

    /// <summary>Creates an XLSX deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="stream">The stream, used from its current position without rewinding.</param>
    /// <param name="leaveOpen">Whether to leave the supplied stream open after the operation. The default is true.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>The supplied stream permits one enumeration across all derived configurations. The library does not rewind it.</remarks>
    public static XlsxSource<T> Xlsx<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream must support reading.", nameof(stream));
        return new(new StreamSource(stream, leaveOpen), new XlsxOptions());
    }

    /// <summary>Creates an XLSX deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream.</remarks>
    public static XlsxSource<T> Xlsx<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new XlsxOptions());
    }

    /// <summary>Creates an XLSX deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream. Only asynchronous enumeration is supported.</remarks>
    public static XlsxSource<T> XlsxAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new XlsxOptions());
    }

    /// <summary>Creates a CSV deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="path">The file path.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>The path is opened separately for each operation.</remarks>
    public static CsvFile<T> Csv<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, new CsvOptions());
    }

    /// <summary>Creates a CSV deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="stream">The stream, used from its current position without rewinding.</param>
    /// <param name="leaveOpen">Whether to leave the supplied stream open after the operation. The default is true.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>The supplied stream permits one enumeration across all derived configurations. The library does not rewind it.</remarks>
    public static CsvSource<T> Csv<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream must support reading.", nameof(stream));
        return new(new StreamSource(stream, leaveOpen), new CsvOptions());
    }

    /// <summary>Creates a CSV deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream.</remarks>
    public static CsvSource<T> Csv<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new CsvOptions());
    }

    /// <summary>Creates a CSV deferred record source.</summary>
    /// <typeparam name="T">The record class marked with TabularRecordAttribute.</typeparam>
    /// <param name="factory">A factory that returns a fresh stream for each operation. The library disposes each returned stream.</param>
    /// <returns>A deferred source. Reading starts when enumeration advances.</returns>
    /// <remarks>Each factory call must return a fresh stream. The library owns and disposes the returned stream. Only asynchronous enumeration is supported.</remarks>
    public static CsvSource<T> CsvAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new CsvOptions());
    }
}
