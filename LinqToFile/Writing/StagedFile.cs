namespace LinqToFile.Writing;

internal static class StagedFile
{
    internal static FileStream Open(string path, FileStream original, bool asynchronous)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 4096,
            Options = FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None)
        };
        // Do not expose a private original through a more permissive temporary copy.
        // The process umask can further restrict these permissions.
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = File.GetUnixFileMode(original.SafeFileHandle) &
                (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                 UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                 UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        return new FileStream(path, options);
    }
}
