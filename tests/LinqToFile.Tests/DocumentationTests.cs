using System.Text.RegularExpressions;
using Xunit;

namespace LinqToFile.Tests;

public sealed class DocumentationTests
{
    [Theory]
    [InlineData("README.md", 2)]
    [InlineData("csv-and-errors.md", 1)]
    public void CSharpExamplesMatchTheRunnableSample(string file, int expectedCount)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Examples");
        var readme = File.ReadAllText(Path.Combine(directory, file));
        var examples = Regex.Matches(readme, "```csharp\\r?\\n(.*?)```", RegexOptions.Singleline);
        Assert.Equal(expectedCount, examples.Count);
        var sources = new[] { "Person.txt", "Program.txt" }
            .Select(name => Normalize(File.ReadAllText(Path.Combine(directory, name)))).ToArray();
        foreach (Match example in examples)
            Assert.Contains(sources, source => source.Contains(Normalize(example.Groups[1].Value), StringComparison.Ordinal));
    }

    private static string Normalize(string text) =>
        string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
