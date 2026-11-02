using System.Collections.Concurrent;

namespace LinqToFile.Writing;

internal sealed class WriteGuard : IDisposable
{
    private static readonly ConcurrentDictionary<string, byte> Active = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly string path;
    private WriteGuard(string path) => this.path = path;
    internal static WriteGuard Acquire(string path)
    {
        if (!Active.TryAdd(path, 0)) throw new InvalidOperationException("A write is already active for this path.");
        return new(path);
    }
    public void Dispose() => Active.TryRemove(path, out _);
}
