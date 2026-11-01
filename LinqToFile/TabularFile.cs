using LinqToFile.Formats.Csv;
using LinqToFile.Sources;

namespace LinqToFile;

public static class TabularFile
{
    public static CsvSource<T> Csv<T>(string path) where T : class, ITabularRecord<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(new StreamSource(path), new CsvOptions());
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
