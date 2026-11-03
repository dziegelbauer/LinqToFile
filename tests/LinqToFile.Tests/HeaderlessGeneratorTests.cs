using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    [Theory]
    [InlineData("[ColumnIndex(-1)] public int A { get; set; }", "LTF008")]
    [InlineData("[ColumnIndex(0)] public int A { get; set; } [ColumnIndex(0)] public int B { get; set; }", "LTF008")]
    [InlineData("[ColumnIndex(0), Ignore] public int A { get; set; }", "LTF004")]
    [InlineData("[ColumnIndex(0)] public int A => 0;", "LTF002")]
    [InlineData("[ColumnIndex(0)] public static int A { get; set; }", "LTF002")]
    public void RejectsInvalidColumnIndexes(string properties, string diagnostic)
    {
        var (result, _) = Generate("using LinqToFile; [TabularRecord] public partial class Model { " + properties + " }");
        Assert.Contains(result.Diagnostics, d => d.Id == diagnostic);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void MissingIndexesCompileAndCoexistingNamesArePreserved()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            [TabularRecord] public partial class Model {
                [Column("id"), ColumnIndex(2)] public int Id { get; init; }
                public string Name { get; set; } = "";
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        string code = Assert.Single(result.GeneratedTrees).GetText().ToString();
        Assert.Contains("int[] {2,-1,", code);
        Assert.Contains("\"id\"", code);
    }

    [Theory]
    [InlineData("", "int[] {2,4,")]
    [InlineData("[ColumnIndex(1)]", "int[] {1,4,")]
    public void IndexOverridesUseNearestAnnotation(string annotation, string expected)
    {
        var (result, output) = Generate("""
            using LinqToFile;
            public class Base {
                [ColumnIndex(2)] public virtual int Id { get; set; }
                [ColumnIndex(4)] public int Other { get; set; }
            }
            [TabularRecord] public partial class Model : Base {
            """ + annotation + " public override int Id { get; set; } }");
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(expected, Assert.Single(result.GeneratedTrees).GetText().ToString());
    }

    [Theory]
    [InlineData("[ColumnIndex(2)]", "[Ignore]")]
    [InlineData("[Ignore]", "[ColumnIndex(2)]")]
    [InlineData("[ColumnIndex(2)]", "[ColumnIndex(4)] public int Other { get; set; } [ColumnIndex(4)]")]
    public void RejectsInheritedIndexConflicts(string baseAnnotation, string derivedAnnotation)
    {
        var (result, _) = Generate("using LinqToFile; public class Base { " + baseAnnotation +
            " public virtual int Id { get; set; } } [TabularRecord] public partial class Model : Base { " +
            derivedAnnotation + " public override int Id { get; set; } }");
        Assert.Contains(result.Diagnostics, d => d.Id is "LTF004" or "LTF008");
    }
}
