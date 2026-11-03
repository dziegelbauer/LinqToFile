using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    [Theory]
    [InlineData("[BooleanTokens(\"Yes\", \"No\")] public bool Value { get; init; }")]
    [InlineData("[BooleanTokens(\"yes\", \"YES\", Comparison=StringComparison.Ordinal)] public bool? Value { get; set; }")]
    [InlineData("[NullToken(\"NULL\")] public string? Value { get; init; }")]
    [InlineData("[NullToken(\"0\")] public int? Value { get; set; }")]
    [InlineData("[NullToken(\"none\")] public DayOfWeek? Value { get; set; }")]
    [InlineData("[NullToken(\"NULL\"), TemporalFormat(\"yyyy-MM-dd\")] public DateOnly? Value { get; init; }")]
    [InlineData("[NullToken(\"NULL\"), BooleanTokens(\"yes\", \"no\")] public bool? Value { get; set; }")]
    public void GeneratesTypedTokenMappingWithoutRuntimeDiscovery(string property)
    {
        var (result, output) = Generate("using System; using LinqToFile; [TabularRecord] public partial class Model { " + property + " }");
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        string code = Assert.Single(result.GeneratedTrees).ToString();
        if (property.Contains("NullToken"))
        {
            Assert.Contains("row.IsNullToken", code);
            Assert.Contains("value.RejectNullToken", code);
        }
        if (property.Contains("BooleanTokens")) Assert.Contains("row.ReadBooleanTokens", code);
        Assert.DoesNotContain("Reflection", code);
        Assert.DoesNotContain("new global::LinqToFile.NullTokenAttribute", code);
    }

    [Theory]
    [InlineData("[BooleanTokens(\"yes\", \"no\")] public int Value { get; set; }")]
    [InlineData("[BooleanTokens(null, \"no\")] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\"yes\", \"\")] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\" yes\", \"no\")] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\"yes\", \"no\\t\")] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\"yes\", \"YES\")] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\"yes\", \"no\", Comparison=StringComparison.CurrentCulture)] public bool Value { get; set; }")]
    [InlineData("[BooleanTokens(\"yes\", \"no\", Comparison=(StringComparison)99)] public bool Value { get; set; }")]
    [InlineData("[NullToken(\"NULL\")] public string Value { get; set; } = \"\";")]
    [InlineData("[NullToken(\"NULL\")] public int Value { get; set; }")]
    [InlineData("[NullToken(\"NULL\")] public object? Value { get; set; }")]
    [InlineData("[NullToken(null)] public string? Value { get; set; }")]
    [InlineData("[NullToken(\"\")] public string? Value { get; set; }")]
    [InlineData("[NullToken(\" \")] public string? Value { get; set; }")]
    [InlineData("[NullToken(\"NULL \")] public string? Value { get; set; }")]
    [InlineData("[NullToken(\"NULL\", Comparison=StringComparison.InvariantCultureIgnoreCase)] public string? Value { get; set; }")]
    public void ReportsInvalidColumnTokens(string property)
    {
        var (result, _) = Generate("using System; using LinqToFile; [TabularRecord] public partial class Model { " + property + " }");
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF007");
        Assert.Empty(result.GeneratedTrees);
    }

    [Theory]
    [InlineData("[BooleanTokens(\"yes\", \"no\")] public bool Value { get; }")]
    [InlineData("[NullToken(\"NULL\")] public static string? Value { get; set; }")]
    public void RejectsTokenAnnotationsOnUnsupportedMembers(string property)
    {
        var (result, _) = Generate("using LinqToFile; [TabularRecord] public partial class Model { " + property + " }");
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF002");
    }

    [Theory]
    [InlineData("Ignore", "NullToken(\"NULL\")")]
    [InlineData("NullToken(\"NULL\")", "Ignore")]
    [InlineData("ColumnConverter(typeof(Codec))", "NullToken(\"NULL\")")]
    [InlineData("NullToken(\"NULL\")", "ColumnConverter(typeof(Codec))")]
    public void RejectsEffectiveInheritedTokenConflicts(string first, string second)
    {
        var (result, _) = Generate($$"""
            using System; using LinqToFile;
            public struct Codec : IColumnConverter<string?> {
                public static bool TryParse(ReadOnlySpan<char> s, ColumnConversionContext c, out string? v) { v=null; return true; }
                public static bool TryFormat(string? v, Span<char> s, ColumnConversionContext c, out int n) { n=0; return true; }
            }
            public class Base { [{{first}}] public virtual string? Value { get; set; } }
            [TabularRecord] public partial class Model : Base { [{{second}}] public override string? Value { get; set; } }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF004");
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void InheritsAndReplacesNearestTokensAndReadsSetterOnlyProperties()
    {
        const string source = """
            using System; using LinqToFile;
            public class Base { [Column("text"), NullToken("NULL")] public virtual string? Value { get; set; } }
            [TabularRecord] public partial class Model : Base { public override string? Value { get; set; } }
            """;
        var (result, output) = Generate(source);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("\"NULL\"", Assert.Single(result.GeneratedTrees).ToString());
        var (replacement, _) = Generate(source.Replace("public override", "[NullToken(\"NONE\")] public override"));
        Assert.Empty(replacement.Diagnostics);
        string code = Assert.Single(replacement.GeneratedTrees).ToString();
        Assert.Contains("\"NONE\"", code);
        Assert.DoesNotContain("\"NULL\"", code);
        var (setter, setterOutput) = Generate("using LinqToFile; [TabularRecord] public partial class Model { [NullToken(\"NULL\")] public string? Value { set {} } }");
        Assert.Empty(setter.Diagnostics);
        Assert.DoesNotContain(setterOutput.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("CanWrite => false", Assert.Single(setter.GeneratedTrees).ToString());
    }
}
