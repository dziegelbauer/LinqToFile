using System.Buffers;

namespace LinqToFile.Formats.Xlsx;

// The package owns these resources. The shared query lease owns the supplied source.
internal static class XlsxInput
{
    internal static XlsxPackage Open(Stream source, XlsxOptions options)
    {
        var resources = new XlsxResources(options);
        try
        {
            if (!source.CanSeek)
            {
                var spool = resources.CreateTemporary();
                byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    long total = 0;
                    int read;
                    while ((read = source.Read(buffer, 0, ReadSize(options, total, buffer.Length))) != 0)
                    {
                        total = CheckWorkbook(options, total, read);
                        resources.ReserveTemporary(read);
                        spool.Write(buffer, 0, read);
                    }
                    spool.Position = 0;
                    source = spool;
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
            }
            return new(source, options, resources);
        }
        catch { resources.Dispose(); throw; }
    }

    internal static async ValueTask<XlsxPackage> OpenAsync(Stream source, XlsxOptions options, CancellationToken token)
    {
        var resources = new XlsxResources(options, token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!source.CanSeek)
            {
                var spool = resources.CreateTemporary();
                byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, ReadSize(options, total, buffer.Length)), token).ConfigureAwait(false)) != 0)
                    {
                        token.ThrowIfCancellationRequested();
                        total = CheckWorkbook(options, total, read);
                        resources.ReserveTemporary(read);
                        await spool.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    }
                    await spool.FlushAsync(token).ConfigureAwait(false);
                    spool.Position = 0;
                    source = spool;
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
            }
            return await XlsxPackage.CreateAsync(source, options, resources, token).ConfigureAwait(false);
        }
        catch { await resources.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private static int ReadSize(XlsxOptions options, long total, int size) =>
        options.MaxWorkbookBytes is long maximum && maximum - total < size ? (int)(maximum - total + 1) : size;

    private static long CheckWorkbook(XlsxOptions options, long total, int read)
    {
        if (total > long.MaxValue - read || options.MaxWorkbookBytes is long maximum && read > maximum - total)
            throw new XlsxFormatException("The workbook-byte limit was exceeded.");
        return total + read;
    }
}
