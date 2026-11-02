using LinqToFile.Formats.Csv;
using LinqToFile.Sources;
using LinqToFile.Formats.Xlsx;

namespace LinqToFile;

public static class TabularFile
{
    public static XlsxSource<T> Xlsx<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(new StreamSource(path), new XlsxOptions());
    }

    public static XlsxSource<T> Xlsx<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream must support reading.", nameof(stream));
        return new(new StreamSource(stream, leaveOpen), new XlsxOptions());
    }

    public static XlsxSource<T> Xlsx<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new XlsxOptions());
    }

    public static XlsxSource<T> XlsxAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new XlsxOptions());
    }

    public static CsvFile<T> Csv<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(path, new CsvOptions());
    }

    public static CsvSource<T> Csv<T>(Stream stream, bool leaveOpen = true) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream must support reading.", nameof(stream));
        return new(new StreamSource(stream, leaveOpen), new CsvOptions());
    }

    public static CsvSource<T> Csv<T>(Func<Stream> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new CsvOptions());
    }

    public static CsvSource<T> CsvAsync<T>(Func<CancellationToken, ValueTask<Stream>> factory) where T : class, ITabularRecord<T>
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(new StreamSource(factory), new CsvOptions());
    }
}
