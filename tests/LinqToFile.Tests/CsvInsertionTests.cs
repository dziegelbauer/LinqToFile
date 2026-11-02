using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using LinqToFile.Formats.Csv;
using LinqToFile.Writing;
using Xunit;

namespace LinqToFile.Tests;

public sealed class CsvInsertionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Insert-" + Guid.NewGuid().ToString("N"));
    public CsvInsertionTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string PathFor(string name = "data.csv") => Path.Combine(directory, name);
    private CsvFile<WriteRow> FileFor(bool inPlace) => TabularFile.Csv<WriteRow>(PathFor()).WithInPlaceWrites(inPlace);
    private void NoTemporaryFiles() => Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));

    public static IEnumerable<object[]> Cases()
    {
        foreach (bool inPlace in new[] { false, true })
        foreach (int input in new[] { 0, 1, 2 })
        foreach (long position in new long[] { 0, 1, 2 })
            yield return [inPlace, input, position];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task InsertsLogicalRowsAfterPrecedingBlankLinesWithoutChangingOldBytes(bool inPlace, int input, long position)
    {
        const string head = "Z,A\r\n\n\r\n";
        const string first = "\"old\r\n雪😀\",7\n\r\n\n";
        const string last = "last,not-an-int";
        const string added = "\"new,\"\"😀\"\"\",8\r\n raw ,9\r\n";
        string original = head + first + last;
        File.WriteAllText(PathFor(), original, new UTF8Encoding(true, true));
        var file = FileFor(inPlace).WithBufferSize(1).WithAppendValidation(false);
        var rows = new[] { new WriteRow { Text = "new,\"😀\"", Number = 8 }, new WriteRow { Text = " raw ", Number = 9 } };
        long count = input switch
        {
            0 => file.InsertRange(position, new Once<WriteRow>(rows)),
            1 => await file.InsertRangeAsync(position, new Once<WriteRow>(rows)),
            _ => await file.InsertRangeAsync(position, AsyncRows(rows))
        };
        string expected = position switch { 0 => head + added + first + last, 1 => head + first + added + last, _ => original + "\r\n" + added };
        Assert.Equal(2, count);
        Assert.Equal(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)), File.ReadAllBytes(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleRowOverloadsHeadersOnlyAndPhysicalEndAreSupported(bool inPlace)
    {
        var file = FileFor(inPlace);
        Assert.Throws<FileNotFoundException>(() => file.Insert(0, new() { Number = 1 }));
        File.WriteAllText(PathFor(), "Z,A");
        Assert.Equal(1, file.Insert(0, new() { Text = "first", Number = 1 }));
        Assert.Equal(1, await file.InsertAsync(0, new() { Text = "before", Number = 2 }));
        Assert.Equal("Z,A\nbefore,2\nfirst,1\n", File.ReadAllText(PathFor()));
        File.AppendAllText(PathFor(), "\r\n\n");
        Assert.Equal(1, await file.InsertAsync(2, new() { Text = "end", Number = 3 }));
        Assert.Equal("Z,A\nbefore,2\nfirst,1\n\r\n\nend,3\n", File.ReadAllText(PathFor()));
        Assert.Equal([2, 1, 3], file.Select(x => x.Number).ToArray()); NoTemporaryFiles();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EmptyInputsStillValidateIndexAndPreserveBytesAndTimestamp(bool inPlace, int input, long position)
    {
        const string original = "Z,A\nfirst,1\nlast,2";
        File.WriteAllText(PathFor(), original);
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(PathFor(), timestamp);
        var file = FileFor(inPlace);
        long result = input switch
        {
            0 => file.InsertRange(position, new Once<WriteRow>([])),
            1 => await file.InsertRangeAsync(position, new Once<WriteRow>([])),
            _ => await file.InsertRangeAsync(position, AsyncRows(Array.Empty<WriteRow>()))
        };
        Assert.Equal(0, result); Assert.Equal(original, File.ReadAllText(PathFor()));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(3L)]
    [InlineData((long)int.MaxValue + 1)]
    [InlineData(long.MaxValue)]
    public async Task InvalidIndexesThrowForEmptyAndNonemptyInputsWithoutMutation(long position)
    {
        const string original = "Z,A\nfirst,1\nlast,2\n";
        File.WriteAllText(PathFor(), original);
        foreach (bool inPlace in new[] { false, true })
        {
            var file = FileFor(inPlace);
            Assert.Throws<ArgumentOutOfRangeException>(() => file.InsertRange(position, []));
            Assert.Throws<ArgumentOutOfRangeException>(() => file.Insert(position, new() { Number = 3 }));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await file.InsertRangeAsync(position, AsyncRows(Array.Empty<WriteRow>())));
            Assert.Equal(original, File.ReadAllText(PathFor())); NoTemporaryFiles();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Z,A,extra\n")]
    [InlineData("Z,A\nx\n")]
    [InlineData("Z,A\nx,1\n\"unterminated")]
    [InlineData("Z,A\nx,1\nbad\"quote,2\n")]
    public async Task AlwaysValidatesTheWholeFileDespiteQuickAppendSetting(string original)
    {
        foreach (bool inPlace in new[] { false, true })
        {
            File.WriteAllText(PathFor(), original);
            var file = FileFor(inPlace).WithAppendValidation(false);
            Assert.ThrowsAny<TabularFileException>(() => file.InsertRange(0, []));
            await Assert.ThrowsAnyAsync<TabularFileException>(async () => await file.InsertAsync(0, new() { Number = 3 }));
            Assert.Equal(original, File.ReadAllText(PathFor())); NoTemporaryFiles();
        }
    }

    public static IEnumerable<object[]> Encodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (Encoding encoding in new Encoding[]
        {
            new UTF8Encoding(false, true), new UTF8Encoding(true, true),
            new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true),
            new UTF32Encoding(false, true, true), new UTF32Encoding(true, true, true), Encoding.Latin1,
            Encoding.GetEncoding(37, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        })
        foreach (bool inPlace in new[] { false, true }) yield return [encoding, inPlace];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public async Task PreservesEncodingBomAndSuffixAcrossUnicodeBoundaries(Encoding encoding, bool inPlace)
    {
        string text = encoding.IsSingleByte ? "old" : "雪😀";
        string prefix = "Value\r\n\"" + text + "\r\ninside\"\n\n";
        string suffix = "last";
        byte[] before = encoding.GetPreamble().Concat(encoding.GetBytes(prefix + suffix)).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace).WithBufferSize(1);
        Assert.Equal(1, await file.InsertAsync(1, new() { Value = text + "new" }));
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(prefix + text + "new\r\n" + suffix)), File.ReadAllBytes(PathFor()));
        Assert.Equal(text + "new", file.ElementAt(1).Value); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomDialectHeaderBindingWhitespaceAndLineOverrideApply(bool inPlace)
    {
        const string original = " z ; a \r\n'first;''snow''';1\r\nlast;2";
        File.WriteAllText(PathFor(), original);
        var file = FileFor(inPlace).WithDelimiter(';').WithQuote('\'').WithHeaderTrimming()
            .WithHeaderComparison(StringComparer.OrdinalIgnoreCase).WithFieldTrimming().WithNewLine(LineEnding.Lf);
        await file.InsertAsync(1, new() { Text = " raw;'😀' ", Number = 3 });
        Assert.Equal(" z ; a \r\n'first;''snow''';1\r\n' raw;''😀'' ';3\nlast;2", File.ReadAllText(PathFor()));
        Assert.Equal([1, 3, 2], file.Select(x => x.Number).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetectedBomAndDisabledDetectionMatchReaderSelection(bool inPlace)
    {
        foreach (bool detect in new[] { false, true })
        {
            var encoding = new UnicodeEncoding(true, false, true);
            byte[] before = new UnicodeEncoding(true, true).GetPreamble().Concat(encoding.GetBytes("Value\n雪\nlast")).ToArray();
            File.WriteAllBytes(PathFor(), before);
            var file = TabularFile.Csv<WriteText>(PathFor()).WithInPlaceWrites(inPlace).WithBomDetection(detect).WithBufferSize(1);
            if (!detect) file = file.WithEncoding(encoding);
            await file.InsertAsync(1, new() { Value = "😀" });
            Assert.Equal(new UnicodeEncoding(true, true).GetPreamble().Concat(encoding.GetBytes("Value\n雪\n😀\nlast")), File.ReadAllBytes(PathFor()));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementFallbackOffsetsUseActualBytesRatherThanReencoding(bool inPlace)
    {
        var encoding = new UTF8Encoding(false, false);
        byte[] before = Encoding.UTF8.GetBytes("Value\n").Concat(new byte[] { 0xff, 0xc2, (byte)'A', (byte)'\n' })
            .Concat(Encoding.UTF8.GetBytes("last")).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace).WithBufferSize(1);
        await file.InsertAsync(1, new() { Value = "new" });
        Assert.Equal(before.Take(10).Concat(Encoding.UTF8.GetBytes("new\n")).Concat(before.Skip(10)), File.ReadAllBytes(PathFor()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeMutationLeavesOriginalUnchanged(bool inPlace)
    {
        const string original = "Z,A\nfirst,1\nlast,2\n";
        File.WriteAllText(PathFor(), original);
        using var cts = new CancellationTokenSource();
        async IAsyncEnumerable<WriteRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            await Task.Yield(); cts.Cancel(); token.ThrowIfCancellationRequested();
            yield return new() { Number = 3 };
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(inPlace).InsertRangeAsync(1, Rows(), cts.Token));
        Assert.Equal(original, File.ReadAllText(PathFor())); NoTemporaryFiles();
        Assert.Equal(1, FileFor(inPlace).Insert(1, new() { Number = 4 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutationCutoffIncludesAsyncInputAndSuffixMovement(bool inPlace)
    {
        const string original = "Z,A\nfirst,1\nlast,2";
        File.WriteAllText(PathFor(), original);
        using var cts = new CancellationTokenSource();
        async IAsyncEnumerable<WriteRow> Rows([EnumeratorCancellation] CancellationToken token = default)
        {
            yield return new() { Text = "new", Number = 3 };
            await Task.Yield(); cts.Cancel(); token.ThrowIfCancellationRequested();
            yield return new() { Text = "next", Number = 4 };
        }
        if (inPlace) Assert.Equal(2, await FileFor(true).InsertRangeAsync(1, Rows(), cts.Token));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await FileFor(false).InsertRangeAsync(1, Rows(), cts.Token));
        Assert.Equal(inPlace ? "Z,A\nfirst,1\nnew,3\nnext,4\nlast,2" : original, File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task InputFailureHonorsModeWithoutReplayOrTemporarySpill(bool inPlace, int input, long position)
    {
        const string original = "Z,A\nfirst,1\nlast,2";
        File.WriteAllText(PathFor(), original);
        IEnumerable<WriteRow> Rows()
        {
            yield return new() { Text = "new", Number = 3 };
            if (inPlace) Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
            throw new InvalidOperationException("input failed");
        }
        var file = FileFor(inPlace);
        if (input == 0) Assert.Throws<InvalidOperationException>(() => file.InsertRange(position, Rows()));
        else if (input == 1) await Assert.ThrowsAsync<InvalidOperationException>(async () => await file.InsertRangeAsync(position, Rows()));
        else await Assert.ThrowsAsync<InvalidOperationException>(async () => await file.InsertRangeAsync(position, AsyncRows(Rows())));
        string partial = original + (position == 2 ? "\n" : "") + "new,3\n";
        Assert.Equal(inPlace ? partial : original, File.ReadAllText(PathFor())); NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeSuffixAndUnknownLengthInputRemainIncremental(bool asynchronous)
    {
        const string header = "Value\n";
        string suffix = string.Concat(Enumerable.Repeat("tail😀雪\n", 50_000));
        File.WriteAllText(PathFor(), header + suffix);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithInPlaceWrites();
        IEnumerable<WriteText> Rows()
        {
            for (int i = 0; i < 5000; i++)
            {
                Assert.Empty(Directory.GetFiles(directory, ".linqtofile-*.tmp"));
                yield return new() { Value = "new" + i };
            }
        }
        long result = asynchronous ? await file.InsertRangeAsync(0, AsyncRows(Rows())) : file.InsertRange(0, new Once<WriteText>(Rows()));
        Assert.Equal(5000, result);
        Assert.Equal(header + string.Concat(Enumerable.Range(0, 5000).Select(i => "new" + i + "\n")) + suffix, File.ReadAllText(PathFor()));
        NoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullsLimitsAndSensitiveErrorsAreCheckedBeforeFirstMutation(bool inPlace)
    {
        const string original = "Value\nfirst\nlast";
        File.WriteAllText(PathFor(), original);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithInPlaceWrites(inPlace);
        Assert.Throws<ArgumentNullException>(() => file.Insert(0, null!));
        Assert.Throws<TabularWriteException>(() => file.InsertRange(0, [null!]));
        var error = await Assert.ThrowsAsync<TabularWriteException>(async () => await file.WithMaxFieldLength(5)
            .InsertAsync(0, new() { Value = "secret-value" }));
        Assert.DoesNotContain("secret-value", error.ToString()); Assert.Null(error.InnerException);
        Assert.Equal(original, File.ReadAllText(PathFor()));
        await file.InsertAsync(1, new() { Value = null });
        Assert.Equal("Value\nfirst\n\"\"\nlast", File.ReadAllText(PathFor()));
    }

    [Fact]
    public async Task OverlappingWritesAreRejectedAndGuardsAreReleased()
    {
        File.WriteAllText(PathFor(), "Z,A\nfirst,1\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<WriteRow> Rows()
        {
            entered.SetResult(); await resume.Task; yield return new() { Number = 2 };
        }
        var pending = FileFor(false).InsertRangeAsync(0, Rows()).AsTask();
        await entered.Task;
        try
        {
            var same = TabularFile.Csv<WriteRow>(Path.Combine(directory, ".", "data.csv"));
            Assert.Throws<InvalidOperationException>(() => same.Append(new() { Number = 3 }));
            Assert.Throws<InvalidOperationException>(() => same.WithInPlaceWrites().InsertRange(0, []));
        }
        finally { resume.SetResult(); await pending; }
        Assert.Equal(1, FileFor(false).Insert(0, new() { Number = 4 })); NoTemporaryFiles();
    }

    private static async IAsyncEnumerable<T> AsyncRows<T>(IEnumerable<T> rows)
    {
        foreach (var row in rows) { await Task.Yield(); yield return row; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsRecordBoundariesSynthesizedInsideOneFallbackWithoutChangingBytes(bool inPlace)
    {
        var encoding = (Encoding)new UTF8Encoding(false, false).Clone();
        encoding.DecoderFallback = new DecoderReplacementFallback("one\ntwo");
        byte[] before = Encoding.UTF8.GetBytes("Value\n").Concat(new byte[] { 0xff }).ToArray();
        File.WriteAllBytes(PathFor(), before);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace).WithBufferSize(1);
        Assert.Equal(2, file.Count());
        await Assert.ThrowsAsync<NotSupportedException>(async () => await file.InsertAsync(1, new() { Value = "new" }));
        Assert.Equal(0, file.InsertRange(1, []));
        Assert.Equal(before, File.ReadAllBytes(PathFor())); NoTemporaryFiles();
        Assert.Equal(1, await file.InsertAsync(0, new() { Value = "new" }));
        Assert.Equal(Encoding.UTF8.GetBytes("Value\nnew\n").Concat(new byte[] { 0xff }), File.ReadAllBytes(PathFor()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IgnoredTrailingBytesDoNotCreateAnExtraBlankRecord(bool inPlace)
    {
        var encoding = (Encoding)new UTF8Encoding(false, false).Clone();
        encoding.DecoderFallback = new DecoderReplacementFallback("");
        byte[] before = Encoding.UTF8.GetBytes("Value\nfirst\n").Concat(new byte[] { 0xff }).ToArray();
        foreach (bool append in new[] { false, true })
        {
            File.WriteAllBytes(PathFor(), before);
            var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace).WithBlankLineSkipping(false);
            if (append) await file.AppendAsync(new() { Value = "new" });
            else await file.InsertAsync(1, new() { Value = "new" });
            Assert.Equal(before.Concat(Encoding.UTF8.GetBytes("new\n")), File.ReadAllBytes(PathFor()));
            Assert.Equal(2, file.Count());
        }
    }

    [Fact]
    public void NumericInsertionAllocationDoesNotGrowWithInputCount()
    {
        var value = new AllocationRow { Value = 42 };
        long Measure(int count, string name)
        {
            string path = PathFor(name); File.WriteAllText(path, "Value\nold-value\n");
            var file = TabularFile.Csv<AllocationRow>(path);
            IEnumerable<AllocationRow> Rows() { for (int i = 0; i < count; i++) yield return value; }
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(count, file.InsertRange(0, Rows()));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatefulInsertionFailsBeforeAnyChangeWhileReadingAndCreationRemainAvailable(bool inPlace)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(50220, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var file = TabularFile.Csv<WriteText>(PathFor()).WithEncoding(encoding).WithInPlaceWrites(inPlace);
        Assert.Equal(1, file.Create([new() { Value = "雪" }]));
        byte[] before = File.ReadAllBytes(PathFor());
        await Assert.ThrowsAsync<NotSupportedException>(async () => await file.InsertAsync(0, new() { Value = "new" }));
        Assert.Equal(before, File.ReadAllBytes(PathFor())); Assert.Equal("雪", file.Single().Value); NoTemporaryFiles();
    }
    private sealed class Once<T>(IEnumerable<T> rows) : IEnumerable<T>
    {
        private bool used;
        public IEnumerator<T> GetEnumerator() { Assert.False(used); used = true; return rows.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
