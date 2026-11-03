using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvOverwriteTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Overwrite-" + Guid.NewGuid().ToString("N"));
    public CsvOverwriteTests() => Directory.CreateDirectory(directory);
    private string Destination => Path.Combine(directory, "data.csv");
    public void Dispose() => Directory.Delete(directory, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverwriteIsExplicitImmutableAndAlsoCreatesMissingFiles(bool asynchronous)
    {
        var original = TabularFile.Csv<WriteRow>(Destination);
        var overwrite = original.WithExistingFileBehavior(ExistingFileBehavior.Overwrite).WithDelimiter(';');
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithExistingFileBehavior((ExistingFileBehavior)99));
        Assert.Equal(0, asynchronous ? await overwrite.CreateAsync() : overwrite.Create());
        Assert.Throws<IOException>(() => original.Create());
        Assert.Equal(1, asynchronous ? await overwrite.CreateAsync([new WriteRow { Number = 7 }]) : overwrite.Create([new WriteRow { Number = 7 }]));
        Assert.Equal("A;Z\n7;\n", File.ReadAllText(Destination));
        Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
    }

    [Fact]
    public async Task FailureAndCancellationPreserveOriginalAndRemoveTemporaryOutput()
    {
        File.WriteAllText(Destination, "original");
        var writer = TabularFile.Csv<WriteRow>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
        IEnumerable<WriteRow> Broken()
        {
            Assert.Equal("original", File.ReadAllText(Destination));
            yield return new() { Number = 1 };
            throw new InvalidOperationException("input failed");
        }
        Assert.Throws<InvalidOperationException>(() => writer.Create(Broken()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreateAsync(Broken()).AsTask());
        using var cancellation = new CancellationTokenSource();
        async IAsyncEnumerable<WriteRow> Cancelled()
        {
            yield return new() { Number = 1 };
            await Task.Yield();
            cancellation.Cancel();
            yield return new() { Number = 2 };
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.CreateAsync(Cancelled(), cancellation.Token).AsTask());
        Assert.Equal("original", File.ReadAllText(Destination));
        Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
    }

    [Fact]
    public async Task InPlaceOverwriteIsRejectedOnlyForCreationInEitherOptionOrder()
    {
        var writer = TabularFile.Csv<WriteRow>(Destination).WithInPlaceWrites().WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
        Assert.Throws<InvalidOperationException>(() => writer.Create());
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreateAsync().AsTask());
        File.WriteAllText(Destination, "A,Z\n");
        Assert.Equal(1, writer.Append(new WriteRow { Number = 3 }));
        Assert.Equal(1, writer.Insert(0, new WriteRow { Number = 2 }));
        Assert.Equal(new[] { 2, 3 }, writer.Select(row => row.Number));
        Assert.Throws<InvalidOperationException>(() => writer.WithInPlaceWrites(false).WithExistingFileBehavior(ExistingFileBehavior.Overwrite).WithInPlaceWrites().Create());
    }

    [Fact]
    public void OverwriteDoesNotWidenUnixPermissions()
    {
        if (OperatingSystem.IsWindows()) return;
        File.WriteAllText(Destination, "private");
        File.SetUnixFileMode(Destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        TabularFile.Csv<WriteRow>(Destination).WithExistingFileBehavior(ExistingFileBehavior.Overwrite).Create();
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Destination));
    }
}
