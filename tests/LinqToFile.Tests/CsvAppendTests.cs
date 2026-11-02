using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Tests.Support;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvAppendTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Append-" + Guid.NewGuid().ToString("N"));
    public CsvAppendTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string PathFor(string name = "data.csv") => Path.Combine(directory, name);
    private void NoTemporaryFiles() => Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
    private CsvFile<WriteRow> FileFor(bool inPlace, bool validate = true) =>
        TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace).WithAppendValidation(validate);

    public static IEnumerable<object[]> Cases()
    {
        foreach (bool inPlace in new[] { false, true })
        foreach (bool validate in new[] { false, true })
        foreach (int input in new[] { 0, 1, 2 })
            yield return [inPlace, validate, input];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AppendsAllInputKindsInFileHeaderOrderAndPreservesEveryOldByte(bool inPlace, bool validate, int input)
    {
        const string original = "Z,A\r\n\"old \r\n雪\",7\n\n'raw',not-an-int"; // Validation must not convert old values.
        File.WriteAllText(PathFor(), original, new UTF8Encoding(true, true));
        var before = File.ReadAllBytes(PathFor());
        var file = FileFor(inPlace, validate).WithBufferSize(1);
        var rows = new[] { new WriteRow { Text = "a,\"b\"\r\n😀", Number = 8 }, new WriteRow { Text = " raw ", Number = 9 } };
        long count = input switch
        {
            0 => file.AppendRange(new Once<WriteRow>(rows)),
            1 => await file.AppendRangeAsync(new Once<WriteRow>(rows)),
            _ => await file.AppendRangeAsync(AsyncRows(rows))
        };
        Assert.Equal(2, count);
        var after = File.ReadAllBytes(PathFor());
        Assert.Equal(before, after.Take(before.Length));
        Assert.Equal("\r\n\"a,\"\"b\"\"\r\n😀\",8\r\n raw ,9\r\n", Encoding.UTF8.GetString(after.AsSpan(before.Length)));
        NoTemporaryFiles();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EmptyRangesValidateButDoNotChangeOrReplaceTheFile(bool inPlace, bool validate, int input)
    {
        const string original = "Z,A\r\nvalue,7";
        File.WriteAllText(PathFor(), original);
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(PathFor(), timestamp);
        var file = FileFor(inPlace, validate);
        long count = input switch
        {
            0 => file.AppendRange(new Once<WriteRow>([])),
            1 => await file.AppendRangeAsync(new Once<WriteRow>([])),
            _ => await file.AppendRangeAsync(AsyncRows(Array.Empty<WriteRow>()))
        };
        Assert.Equal(0, count);
        Assert.Equal(original, File.ReadAllText(PathFor()));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleRowVariantsReturnOneAndRequireAnExistingDestination(bool inPlace)
    {
        var file = FileFor(inPlace);
        Assert.Throws<FileNotFoundException>(() => file.Append(new() { Number = 1 }));
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await file.AppendAsync(new() { Number = 1 }));
        Assert.Throws<FileNotFoundException>(() => file.AppendRange([]));
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await file.AppendRangeAsync(Array.Empty<WriteRow>()));
        Assert.False(File.Exists(PathFor()));
        file.Create();
        Assert.Equal(1, file.Append(new() { Number = 1, Text = "a" }));
        Assert.Equal(1, await file.AppendAsync(new() { Number = 2, Text = "b" }));
        Assert.Equal([1, 2], file.Select(x => x.Number).ToArray());
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\r\n")]
    [InlineData("A\n")]
    [InlineData("A,A\n")]
    [InlineData("Z,A,extra\n")]
    [InlineData("Z,A, \n")]
    [InlineData("\"Z,A\n")]
    public async Task InvalidHeadersFailEvenForEmptyRangesAndQuickAppend(string original)
    {
        foreach (bool inPlace in new[] { false, true })
        foreach (bool validate in new[] { false, true })
        {
            File.WriteAllText(PathFor(), original);
            var file = FileFor(inPlace, validate).WithBufferSize(1);
            Assert.ThrowsAny<TabularFileException>(() => file.AppendRange([]));
            await Assert.ThrowsAnyAsync<TabularFileException>(async () => await file.AppendRangeAsync(AsyncRows(Array.Empty<WriteRow>())));
            Assert.Equal(original, File.ReadAllText(PathFor()));
            NoTemporaryFiles();
        }
    }

    [Theory]
    [InlineData("Z,A\nx\n")]
    [InlineData("Z,A\nx,1,extra\n")]
    [InlineData("Z,A\n\"bad,1")]
    [InlineData("Z,A\nbad\"quote,1\n")]
    [InlineData("Z,A\n\"bad\" ,1\n")]
    public async Task DefaultValidationRejectsOldSyntaxButQuickModeSkipsIt(string original)
    {
        foreach (bool inPlace in new[] { false, true })
        {
            File.WriteAllText(PathFor(), original);
            var file = FileFor(inPlace);
            Assert.Throws<CsvFormatException>(() => file.AppendRange([]));
            await Assert.ThrowsAsync<CsvFormatException>(async () => await file.AppendAsync(new() { Number = 8 }));
            Assert.Equal(original, File.ReadAllText(PathFor()));
            Assert.Equal(1, await file.WithAppendValidation(false).AppendAsync(new() { Number = 8, Text = "new" }));
            Assert.StartsWith(original, File.ReadAllText(PathFor()), StringComparison.Ordinal);
            NoTemporaryFiles();
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("")]
    public async Task HeaderDeterminesNewLineUnlessExplicitlyOverridden(string ending)
    {
        File.WriteAllText(PathFor(), "Value" + ending);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithBufferSize(1);
        await file.AppendAsync(new() { Value = "x" });
        string effective = ending == "" ? "\n" : ending;
        Assert.Equal("Value" + effective + "x" + effective, File.ReadAllText(PathFor()));
        await file.WithNewLine(LineEnding.Lf).AppendAsync(new() { Value = "y" });
        Assert.EndsWith("y\n", File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeaderComparisonTrimmingAndCustomDialectApplyToAppend(bool inPlace)
    {
        const string original = " z ; a \r\n'old;''雪''';7\r\n";
        File.WriteAllText(PathFor(), original);
        var file = FileFor(inPlace).WithHeaderComparison(StringComparer.OrdinalIgnoreCase)
            .WithHeaderTrimming().WithFieldTrimming().WithDelimiter(';').WithQuote('\'').WithBufferSize(1);
        Assert.Equal(1, await file.AppendAsync(new() { Number = 8, Text = " raw;'😀' " }));
        Assert.Equal(original + "' raw;''😀'' ';8\r\n", File.ReadAllText(PathFor()));
        Assert.Equal("raw;'😀'", file.Last().Text);
        NoTemporaryFiles();
    }

    public static IEnumerable<object[]> Encodings()
    {
        foreach (Encoding encoding in new Encoding[]
        {
            new UTF8Encoding(false, true), new UTF8Encoding(true, true),
            new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true),
            new UTF32Encoding(false, true, true), new UTF32Encoding(true, true, true), Encoding.Latin1, Encoding.ASCII
        })
        foreach (bool inPlace in new[] { false, true })
        foreach (bool validate in new[] { false, true })
            yield return [encoding, inPlace, validate];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public async Task EncodingBomAndSplitUnicodeBoundariesArePreserved(Encoding encoding, bool inPlace, bool validate)
    {
        string text = encoding.IsSingleByte ? "old" : "雪😀";
        var before = encoding.GetPreamble().Concat(encoding.GetBytes("Value\r\n" + text)).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace)
            .WithAppendValidation(validate).WithBufferSize(1);
        Assert.Equal(1, await file.AppendAsync(new() { Value = text + "new" }));
        byte[] expected = before.Concat(encoding.GetBytes("\r\n" + text + "new\r\n")).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(PathFor()));
        Assert.Equal<string>([text, text + "new"], file.Select(x => x.Value!));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetectedBomOverridesConfiguredEncodingAndStillUsesItsStrictEncoder(bool inPlace)
    {
        var encoding = new UnicodeEncoding(true, true, true);
        byte[] before = encoding.GetPreamble().Concat(encoding.GetBytes("Value\n雪")).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithInPlaceWrites(inPlace);
        Assert.Equal(1, await file.AppendAsync(new() { Value = "😀" }));
        Assert.Equal(before.Concat(encoding.GetBytes("\n😀\n")), File.ReadAllBytes(PathFor()));
        byte[] valid = File.ReadAllBytes(PathFor());
        await Assert.ThrowsAsync<TabularWriteException>(async () => await file.WithInPlaceWrites(false)
            .AppendAsync(new() { Value = "\ud800" }));
        Assert.Equal(valid, File.ReadAllBytes(PathFor()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledBomDetectionUsesTheConfiguredEncodingWithoutAddingAnotherBom(bool inPlace)
    {
        var encoding = new UnicodeEncoding(false, false, true);
        byte[] before = Encoding.Unicode.GetPreamble().Concat(encoding.GetBytes("Value\n雪")).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithBomDetection(false)
            .WithInPlaceWrites(inPlace).WithAppendValidation(false).WithBufferSize(1);
        await file.AppendAsync(new() { Value = "new" });
        Assert.Equal(before.Concat(encoding.GetBytes("\nnew\n")), File.ReadAllBytes(PathFor()));
        Assert.Equal("new", file.Last().Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedStatefulEncodingIsRejectedBeforeMutation(bool inPlace)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(50220, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace);
        file.Create([new() { Value = "雪" }]); // The restriction does not affect creation or reading.
        Assert.Equal("雪", file.Single().Value);
        byte[] before = File.ReadAllBytes(PathFor());
        Assert.Throws<NotSupportedException>(() => file.AppendRange([]));
        await Assert.ThrowsAsync<NotSupportedException>(async () => await file.WithAppendValidation(false).AppendAsync(new() { Value = "new" }));
        Assert.Equal(before, File.ReadAllBytes(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuickAppendDoesNotDecodeInvalidBodyBytes(bool inPlace)
    {
        foreach (var ending in new[] { "\n", "\r\n", "\r" })
        foreach (int bufferSize in new[] { 1, 4096 })
        {
            byte[] before = Encoding.UTF8.GetBytes("A" + ending).Concat(new byte[] { 0xff, 0xff }).ToArray();
            File.WriteAllBytes(PathFor(), before);
            var file = TabularFile.Csv<AppendNumber>(PathFor()).WithInPlaceWrites(inPlace).WithBufferSize(bufferSize);
            await Assert.ThrowsAsync<DecoderFallbackException>(async () => await file.AppendRangeAsync(Array.Empty<AppendNumber>()));
            Assert.Equal(before, File.ReadAllBytes(PathFor()));
            Assert.Equal(1, await file.WithAppendValidation(false).AppendAsync(new() { Value = 42 }));
            Assert.Equal(before, File.ReadAllBytes(PathFor()).Take(before.Length));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeFirstMutationLeavesFileUnchanged(bool inPlace)
    {
        File.WriteAllText(PathFor(), "Z,A\nx,1");
        byte[] before = File.ReadAllBytes(PathFor());
        using var cts = new CancellationTokenSource();
        async IAsyncEnumerable<WriteRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield();
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            yield return new() { Number = 2 };
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(inPlace).AppendRangeAsync(Rows(), cts.Token));
        Assert.Equal(before, File.ReadAllBytes(PathFor())); NoTemporaryFiles();
        Assert.Equal(1, FileFor(inPlace).Append(new() { Number = 3 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterFirstWriteStopsOnlyTemporaryModeIncludingAlreadyOpenedInput(bool inPlace)
    {
        const string original = "Z,A\nx,1\n";
        File.WriteAllText(PathFor(), original);
        using var cts = new CancellationTokenSource();
        bool disposed = false;
        async IAsyncEnumerable<WriteRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            try
            {
                yield return new() { Number = 2 };
                await Task.Yield();
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                yield return new() { Number = 3 };
            }
            finally { disposed = true; }
        }
        if (inPlace) Assert.Equal(2, await FileFor(true).AppendRangeAsync(Rows(), cts.Token));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(false).AppendRangeAsync(Rows(), cts.Token));
        Assert.True(disposed);
        Assert.Equal(inPlace ? original + ",2\n,3\n" : original, File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousAsyncInputCancellationUsesTheSameBoundary(bool inPlace)
    {
        const string original = "Z,A\nx,1\n";
        File.WriteAllText(PathFor(), original);
        using var cts = new CancellationTokenSource();
        IEnumerable<WriteRow> Rows()
        {
            yield return new() { Number = 2 };
            cts.Cancel();
            yield return new() { Number = 3 };
        }
        if (inPlace) Assert.Equal(2, await FileFor(true).AppendRangeAsync(Rows(), cts.Token));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(false).AppendRangeAsync(Rows(), cts.Token));
        Assert.Equal(inPlace ? original + ",2\n,3\n" : original, File.ReadAllText(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task InputFailuresPreserveOriginalOrLeaveAcceptedDirectPrefix(bool inPlace, bool validate, int input)
    {
        const string original = "Z,A\nold,1\n";
        File.WriteAllText(PathFor(), original);
        IEnumerable<WriteRow> Rows()
        {
            yield return new() { Number = 2, Text = "new" };
            throw new InvalidOperationException("input failed");
        }
        var file = FileFor(inPlace, validate);
        if (input == 0) Assert.Throws<InvalidOperationException>(() => file.AppendRange(Rows()));
        else if (input == 1) await Assert.ThrowsAsync<InvalidOperationException>(async () => await file.AppendRangeAsync(Rows()));
        else await Assert.ThrowsAsync<InvalidOperationException>(async () => await file.AppendRangeAsync(AsyncRows(Rows())));
        Assert.Equal(inPlace ? original + "new,2\n" : original, File.ReadAllText(PathFor()));
        NoTemporaryFiles();
        Assert.Equal(1, file.Append(new() { Number = 3 }));
    }

    [Fact]
    public async Task OverlappingWritesFailAcrossOptionsAndNormalizedPaths()
    {
        File.WriteAllText(PathFor(), "Z,A\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<WriteRow> Rows()
        {
            entered.SetResult(); await resume.Task;
            yield return new() { Number = 1 };
        }
        var pending = FileFor(false).AppendRangeAsync(Rows()).AsTask();
        await entered.Task;
        try
        {
            var same = TabularFile.Csv<WriteRow>(Path.Combine(directory, ".", "data.csv"));
            Assert.Throws<InvalidOperationException>(() => same.WithInPlaceWrites().Append(new() { Number = 2 }));
            Assert.Throws<InvalidOperationException>(() => same.Create());
        }
        finally { resume.SetResult(); await pending; }
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputChecksStillApplyToQuickAppend(bool inPlace)
    {
        const string original = "Value\nold\n";
        File.WriteAllText(PathFor(), original);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithInPlaceWrites(inPlace).WithAppendValidation(false);
        await Assert.ThrowsAsync<TabularWriteException>(async () => await file.WithMaxFieldLength(5).AppendAsync(new() { Value = "123456" }));
        await Assert.ThrowsAsync<TabularWriteException>(async () => await file.WithMaxRecordLength(5).AppendAsync(new() { Value = "a\"b" }));
        await Assert.ThrowsAsync<TabularWriteException>(async () => await file.AppendRangeAsync(new WriteText[] { null! }));
        Assert.Equal(original, File.ReadAllText(PathFor()));
        await file.AppendAsync(new() { Value = null });
        Assert.Equal(original + "\"\"\n", File.ReadAllText(PathFor())); NoTemporaryFiles();
    }

    [Fact]
    public async Task WriteDiagnosticsUseInputRowAndActualFileColumnOrder()
    {
        const string original = "z,a\nold,1\n";
        File.WriteAllText(PathFor(), original);
        var file = FileFor(false).WithHeaderComparison(StringComparer.OrdinalIgnoreCase).WithMaxFieldLength(4);
        var error = await Assert.ThrowsAsync<TabularWriteException>(async () => await file.AppendRangeAsync(new[]
        {
            new WriteRow { Text = "ok", Number = 2 }, new WriteRow { Text = "secret-value", Number = 3 }
        }));
        Assert.Equal(2, error.RecordNumber); Assert.Equal(1, error.ColumnIndex);
        Assert.Equal("z", error.ColumnHeader); Assert.Equal("Text", error.TargetMember);
        Assert.DoesNotContain("secret-value", error.ToString()); Assert.Null(error.InnerException);
        Assert.Equal(original, File.ReadAllText(PathFor())); NoTemporaryFiles();
    }

    private static async IAsyncEnumerable<T> AsyncRows<T>(IEnumerable<T> rows)
    {
        foreach (var row in rows) { await Task.Yield(); yield return row; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuickPreparationReadsOnlyTheHeaderAndUsesAsyncIoWhenRequested(bool asynchronous)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Value\r\n" + new string('x', 100_000));
        using var stream = new ProbeStream(bytes, chunkSize: 1, asyncOnly: asynchronous, yieldReads: asynchronous);
        var options = new CsvOptions { ValidateAppend = false, BufferSize = 4096 };
        var prepared = asynchronous
            ? await CsvWritePreparation<WriteText>.ReadAsync(stream, options, default)
            : CsvWritePreparation<WriteText>.Read(stream, options);
        Assert.Equal("\r\n", prepared.Separator);
        Assert.InRange(stream.SyncReads + stream.AsyncReads, 1, 12);
        if (asynchronous) Assert.Equal(0, stream.SyncReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyInputStillHonorsCancellationDuringDisposal(bool inPlace)
    {
        const string original = "Z,A\nold,1\n";
        File.WriteAllText(PathFor(), original);
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(inPlace)
            .AppendRangeAsync(new CancelOnDispose(cts), cts.Token));
        Assert.Equal(original, File.ReadAllText(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputDisposalFailureOccursBeforeReplacement(bool inPlace)
    {
        const string original = "Z,A\nold,1\n";
        File.WriteAllText(PathFor(), original);
        Assert.Throws<InvalidOperationException>(() => FileFor(inPlace).AppendRange(new FailOnDispose()));
        Assert.Equal(inPlace ? original + "new,2\n" : original, File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Fact]
    public void PrivateUnixPermissionsAreNotBroadenedByTheStagedCopy()
    {
        if (OperatingSystem.IsWindows()) return;
        File.WriteAllText(PathFor(), "Z,A\nold,1\n");
        File.SetUnixFileMode(PathFor(), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        IEnumerable<WriteRow> Rows()
        {
            yield return new() { Number = 2 };
            string staged = Assert.Single(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
            Assert.Equal(0, (int)(File.GetUnixFileMode(staged) & (UnixFileMode.OtherRead | UnixFileMode.GroupRead)));
        }
        Assert.Equal(1, FileFor(false).AppendRange(Rows()));
        Assert.Equal(0, (int)(File.GetUnixFileMode(PathFor()) & (UnixFileMode.OtherRead | UnixFileMode.GroupRead)));
        NoTemporaryFiles();
    }

    [Fact]
    public void OpenReaderUsesNativeReplacementRulesWithoutFallback()
    {
        const string original = "Z,A\nold,1\n";
        File.WriteAllText(PathFor(), original);
        using var reader = new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<IOException>(() => FileFor(false).Append(new() { Number = 2 }));
            Assert.Equal(original, File.ReadAllText(PathFor()));
        }
        else
        {
            Assert.Equal(1, FileFor(false).Append(new() { Number = 2 }));
            Assert.Equal(original + ",2\n", File.ReadAllText(PathFor()));
            using var text = new StreamReader(reader, leaveOpen: true);
            Assert.Equal(original, text.ReadToEnd());
        }
        NoTemporaryFiles();
    }

    [Fact]
    public async Task MultilineHeaderInfersItsOuterTerminatorAndPreservesSkippedBlankLines()
    {
        const string original = "\n\r\n\"Long\r\nName\"\r\"first\"\n\r\n";
        File.WriteAllText(PathFor(), original);
        var file = TabularFile.Csv<AppendMultilineHeader>(PathFor()).WithAppendValidation(false).WithBufferSize(1);
        Assert.Equal(1, await file.AppendAsync(new() { Value = "second" }));
        Assert.Equal(original + "second\r", File.ReadAllText(PathFor()));
        Assert.Equal("second", file.Last().Value);
    }

    [Fact]
    public async Task SingleByteEncodingDoesNotNeedAsciiNewLineBytes()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(37, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        byte[] before = encoding.GetBytes("Value\r\nold");
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithAppendValidation(false);
        await file.AppendAsync(new() { Value = "new" });
        Assert.Equal(before.Concat(encoding.GetBytes("\r\nnew\r\n")), File.ReadAllBytes(PathFor()));
        Assert.Equal("new", file.Last().Value);
    }

    [Fact]
    public void NumericAppendAllocationDoesNotGrowWithInputRowCount()
    {
        var value = new AllocationRow { Value = 42 };
        long Measure(int count, string name)
        {
            var path = PathFor(name);
            File.WriteAllText(path, "Value\nold-value\n");
            var file = TabularFile.Csv<AllocationRow>(path);
            IEnumerable<AllocationRow> Rows() { for (int i = 0; i < count; i++) yield return value; }
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(count, file.AppendRange(Rows()));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(100_000, "warm");
        long small = long.MaxValue, large = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            small = Math.Min(small, Measure(1000, "small" + i));
            large = Math.Min(large, Measure(100_000, "large" + i));
        }
        Assert.InRange(large - small, -4096, 4096);
    }

    private sealed class CancelOnDispose(CancellationTokenSource cancellation) : IAsyncEnumerable<WriteRow>, IAsyncEnumerator<WriteRow>
    {
        public WriteRow Current => throw new InvalidOperationException();
        public IAsyncEnumerator<WriteRow> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => new(false);
        public ValueTask DisposeAsync() { cancellation.Cancel(); return default; }
    }
    private sealed class FailOnDispose : IEnumerable<WriteRow>, IEnumerator<WriteRow>
    {
        private bool moved;
        public WriteRow Current => new() { Text = "new", Number = 2 };
        object IEnumerator.Current => Current;
        public IEnumerator<WriteRow> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() { bool next = !moved; moved = true; return next; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => throw new InvalidOperationException("Input disposal failed.");
    }
    private sealed class Once<T>(IEnumerable<T> rows) : IEnumerable<T>
    {
        private bool used;
        public IEnumerator<T> GetEnumerator() { Assert.False(used); used = true; return rows.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
[TabularRecord]
public sealed partial class AppendNumber { [Column("A")] public int Value { get; init; } }
[TabularRecord]
public sealed partial class AppendMultilineHeader { [Column("Long\r\nName")] public string? Value { get; init; } }
