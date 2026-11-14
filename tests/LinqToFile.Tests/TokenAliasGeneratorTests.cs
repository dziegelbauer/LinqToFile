using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    [Theory]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = null)")]
    [InlineData("BooleanTokens(\"yes\", \"no\", FalseAliases = new string[] { null })")]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \"\" })")]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \" y\" })")]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \"NO\" })")]
    [InlineData("BooleanTokens(\"yes\", \"no\", FalseAliases = new[] { \"YES\" })")]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \"Y\" }, FalseAliases = new[] { \"y\" })")]
    [InlineData("NullToken(\"NULL\", Aliases = null)")]
    [InlineData("NullToken(\"NULL\", Aliases = new string[] { null })")]
    [InlineData("NullToken(\"NULL\", Aliases = new[] { \"\" })")]
    [InlineData("NullToken(\"NULL\", Aliases = new[] { \"N/A\\t\" })")]
    public void RejectsInvalidAndAmbiguousAliases(string annotation)
    {
        var (result, _) = Generate("using LinqToFile; [TabularRecord] public partial class Model { [" + annotation + "] public bool? Value { get; set; } }");
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF007");
        Assert.Empty(result.GeneratedTrees);
    }

    [Theory]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \"Y\", \"y\", \"yes\" }, FalseAliases = new string[0])")]
    [InlineData("BooleanTokens(\"yes\", \"no\", TrueAliases = new[] { \"Y\" }, FalseAliases = new[] { \"y\" }, Comparison = StringComparison.Ordinal)")]
    [InlineData("NullToken(\"NULL\", Aliases = new[] { \"NULL\", \"N/A\", \"N/A\" })")]
    public void AcceptsEquivalentAliasesForTheSameValue(string annotation)
    {
        var (result, output) = Generate("using System; using LinqToFile; [TabularRecord] public partial class Model { [" + annotation + "] public bool? Value { get; set; } }");
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
    }

    [Fact]
    public void InheritsAliasesAndReplacesTheWholeAnnotation()
    {
        const string source = """
            using LinqToFile;
            public class Base {
                [BooleanTokens("yes", "no", TrueAliases = new[] { "old-true" })]
                [NullToken("NULL", Aliases = new[] { "old-null" })]
                public virtual bool? Value { get; set; }
            }
            [TabularRecord] public partial class Model : Base { public override bool? Value { get; set; } }
            """;
        var (inherited, output) = Generate(source);
        Assert.Empty(inherited.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        string code = Assert.Single(inherited.GeneratedTrees).ToString();
        Assert.Contains("old-true", code);
        Assert.Contains("old-null", code);
        var (replaced, replacementOutput) = Generate(source.Replace("public override", "[BooleanTokens(\"on\", \"off\"), NullToken(\"none\")] public override"));
        Assert.Empty(replaced.Diagnostics);
        Assert.DoesNotContain(replacementOutput.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        string replacement = Assert.Single(replaced.GeneratedTrees).ToString();
        Assert.DoesNotContain("old-true", replacement);
        Assert.DoesNotContain("old-null", replacement);
    }
}
