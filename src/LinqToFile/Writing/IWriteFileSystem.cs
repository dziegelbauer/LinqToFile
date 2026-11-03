namespace LinqToFile.Writing;

// Internal file boundary permits deterministic I/O/commit fault tests without global hooks.
internal interface IWriteFileSystem
{
    Stream OpenExisting(string path, bool inPlace, bool asynchronous);
    Stream OpenStaged(string path, Stream original, bool asynchronous);
    void Replace(string staged, string destination);
    void DeleteTemporary(string path);
}

internal sealed class PhysicalWriteFileSystem : IWriteFileSystem
{
    internal static readonly PhysicalWriteFileSystem Instance = new();
    private PhysicalWriteFileSystem() { }
    public Stream OpenExisting(string path, bool inPlace, bool asynchronous) =>
        new FileStream(path, FileMode.Open, inPlace ? FileAccess.ReadWrite : FileAccess.Read,
            inPlace ? FileShare.None : FileShare.Read, 4096,
            FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
    public Stream OpenStaged(string path, Stream original, bool asynchronous) =>
        StagedFile.Open(path, (FileStream)original, asynchronous);
    public void Replace(string staged, string destination) => File.Replace(staged, destination, null);
    public void DeleteTemporary(string path) => File.Delete(path);
}
