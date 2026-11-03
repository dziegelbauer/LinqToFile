using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    [Theory]
    [InlineData("DateTime", "yyyy-MM-dd HH:mm:ss.fffffff")]
    [InlineData("DateTime?", "O")]
    [InlineData("DateTime", "d")]
    [InlineData("DateTime", "D")]
    [InlineData("DateTime", "f")]
    [InlineData("DateTime", "F")]
    [InlineData("DateTime", "g")]
    [InlineData("DateTime", "G")]
    [InlineData("DateTime", "s")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd'T'HH:mm:sszzz")]
    [InlineData("DateTimeOffset?", "O")]
    [InlineData("DateTimeOffset", "yyyyMMddHHmmssK")]
    [InlineData("DateOnly", "dd MMMM yyyy")]
    [InlineData("DateOnly?", "d")]
    [InlineData("DateOnly", "R")]
    [InlineData("TimeOnly", "HH:mm")]
    [InlineData("TimeOnly?", "O")]
    [InlineData("TimeOnly", "r")]
    [InlineData("TimeOnly", "t")]
    [InlineData("TimeOnly", "T")]
    public void GeneratesExactTemporalParsingFormattingAndCultureValidation(string type, string format)
    {
        var (result, output) = Generate($$"""
            using System; using LinqToFile;
            [TabularRecord] public partial class Model {
                [TemporalFormat("{{format}}")] public {{type}} Value { get; init; }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        string code = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("Exact(ordinals[0]", code);
        Assert.Contains("TemporalFormats.Validate", code);
        Assert.Contains("value.Set(", code);
    }

    [Theory]
    [InlineData("int", "yyyy-MM-dd")]
    [InlineData("TimeSpan", "c")]
    [InlineData("DateOnly", "yyyy-MM")]
    [InlineData("DateOnly", "MM-dd")]
    [InlineData("DateOnly", "yyyy-MM-ddd")]
    [InlineData("DateOnly", "yyyy-MM-dd HH:mm")]
    [InlineData("TimeOnly", "yyyy-MM-dd HH:mm")]
    [InlineData("DateTime", "HH:mm")]
    [InlineData("DateTime", "yyyy-MM-dd zzz")]
    [InlineData("DateTime", "yyyy-MM-dd '")]
    [InlineData("DateTime", "yyyy-MM-dd\\")]
    [InlineData("DateTime", "yyyy-MM-dd %%")]
    [InlineData("DateTime", "yyyy-MM-dd ffffffff")]
    [InlineData("DateTime", "yyyy-MM-dd'\0'")]
    [InlineData("DateTime", "U")]
    [InlineData("DateTime", "R")]
    [InlineData("DateTime", "u")]
    [InlineData("DateTime", "M")]
    [InlineData("DateOnly", "Y")]
    [InlineData("TimeOnly", "s")]
    [InlineData("DateTimeOffset", "d")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd HH:mm")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd HH:mm z")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd HH:mm zz")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd HH:mm KK")]
    [InlineData("DateTimeOffset", "yyyy-MM-dd 'zzz'")]
    [InlineData("DateTime", "")]
    [InlineData("DateTime", " ")]
    [InlineData("DateTime", null)]
    public void ReportsUnsupportedTemporalFormats(string type, string? format)
    {
        string literal = format is null ? "null" : Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(format, true);
        var (result, _) = Generate($$"""
            using System; using LinqToFile;
            [TabularRecord] public partial class Model { [TemporalFormat({{literal}})] public {{type}} Value { get; set; } }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF006");
        Assert.Empty(result.GeneratedTrees);
    }

    [Theory]
    [InlineData("Ignore", "TemporalFormat(\"yyyy-MM-dd\")")]
    [InlineData("TemporalFormat(\"yyyy-MM-dd\")", "Ignore")]
    [InlineData("ColumnConverter(typeof(Codec))", "TemporalFormat(\"yyyy-MM-dd\")")]
    [InlineData("TemporalFormat(\"yyyy-MM-dd\")", "ColumnConverter(typeof(Codec))")]
    public void RejectsInheritedCrossAnnotationConflicts(string first, string second)
    {
        var (result, _) = Generate($$"""
            using System; using LinqToFile;
            public struct Codec : IColumnConverter<DateTime> {
                public static bool TryParse(ReadOnlySpan<char> s, ColumnConversionContext c, out DateTime v) { v=default; return true; }
                public static bool TryFormat(DateTime v, Span<char> s, ColumnConversionContext c, out int n) { n=0; return true; }
            }
            public class Base { [{{first}}] public virtual DateTime Value { get; set; } }
            [TabularRecord] public partial class Model : Base { [{{second}}] public override DateTime Value { get; set; } }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF004");
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void InheritsAndReplacesTemporalFormatsAndPreservesSetterOnlyReading()
    {
        const string source = """
            using System; using LinqToFile;
            public class Base { [Column("date"), TemporalFormat("yyyy-MM-dd")] public virtual DateOnly Value { get; set; } }
            [TabularRecord] public partial class Model : Base { public override DateOnly Value { get; set; } }
            """;
        var (result, output) = Generate(source);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("\"yyyy-MM-dd\"", Assert.Single(result.GeneratedTrees).ToString());
        var (replacement, _) = Generate(source.Replace("public override", "[TemporalFormat(\"dd/MM/yyyy\")] public override"));
        Assert.Empty(replacement.Diagnostics);
        string code = Assert.Single(replacement.GeneratedTrees).ToString();
        Assert.Contains("\"dd/MM/yyyy\"", code);
        Assert.DoesNotContain("\"yyyy-MM-dd\"", code);
        var (setter, setterOutput) = Generate("using System; using LinqToFile; [TabularRecord] public partial class Model { [TemporalFormat(\"O\")] public DateTime Value { set {} } }");
        Assert.Empty(setter.Diagnostics);
        Assert.DoesNotContain(setterOutput.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("CanWrite => false", Assert.Single(setter.GeneratedTrees).ToString());
    }
}
