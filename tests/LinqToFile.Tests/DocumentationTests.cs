using System.Text.RegularExpressions;
using Xunit;

namespace LinqToFile.Tests;

public sealed class DocumentationTests
{
    [Theory]
    [InlineData("README.md", 5)]
    [InlineData("csv-and-errors.md", 1)]
    [InlineData("conversions.md", 1)]
    [InlineData("sources-and-async.md", 2)]
    [InlineData("xlsx.md", 4)]
    [InlineData("csv-writing.md", 3)]
    [InlineData("custom-converters.md", 2)]
    [InlineData("temporal-formats.md", 2)]
    public void CSharpExamplesMatchTheRunnableSample(string file, int expectedCount)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Examples");
        var readme = File.ReadAllText(Path.Combine(directory, file));
        var examples = Regex.Matches(readme, "```csharp\\r?\\n(.*?)```", RegexOptions.Singleline);
        Assert.Equal(expectedCount, examples.Count);
        var sources = new[] { "Person.txt", "Program.txt", "WorkbookEntry.txt", "WorkbookSample.txt", "WorkbookCellsSample.txt", "WorkbookAsyncSample.txt", "CsvWritingSample.txt", "QuickStartSample.txt", "Product.txt", "ConverterSample.txt", "Appointment.txt", "TemporalFormatSample.txt" }
            .Select(name => Normalize(File.ReadAllText(Path.Combine(directory, name)))).ToArray();
        foreach (Match example in examples)
            Assert.Contains(sources, source => source.Contains(Normalize(example.Groups[1].Value), StringComparison.Ordinal));
    }

    private static string Normalize(string text) =>
        string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
