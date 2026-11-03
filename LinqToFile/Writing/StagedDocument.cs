namespace LinqToFile.Writing;

internal sealed class StagedDocument : IDisposable
{
    private readonly string destination;
    private readonly string temporary;
    private readonly ExistingFileBehavior behavior;
    private readonly WriteGuard guard;
    private bool owned;

    internal StagedDocument(string path, ExistingFileBehavior behavior)
    {
        destination = Path.GetFullPath(path);
        this.behavior = behavior;
        temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".linqtofile-" + Guid.NewGuid().ToString("N") + ".tmp");
        guard = WriteGuard.Acquire(destination);
        try
        {
            if (Directory.Exists(destination) || (behavior == ExistingFileBehavior.Throw && File.Exists(destination)))
                throw new IOException("The creation destination already exists.");
        }
        catch { guard.Dispose(); throw; }
    }

    internal FileStream Open(bool asynchronous)
    {
        var settings = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 4096,
            Options = FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None)
        };
        if (!OperatingSystem.IsWindows() && behavior == ExistingFileBehavior.Overwrite && File.Exists(destination))
            settings.UnixCreateMode = File.GetUnixFileMode(destination) &
                (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                 UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        var stream = new FileStream(temporary, settings);
        owned = true;
        return stream;
    }

    internal void Publish(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        File.Move(temporary, destination, behavior == ExistingFileBehavior.Overwrite);
        owned = false;
    }

    public void Dispose()
    {
        try { if (owned) File.Delete(temporary); }
        finally { guard.Dispose(); }
    }
}
