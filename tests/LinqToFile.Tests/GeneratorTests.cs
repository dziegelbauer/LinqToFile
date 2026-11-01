using LinqToFile.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace LinqToFile.Tests;

public sealed class GeneratorTests
{
    private static (GeneratorDriverRunResult Result, Compilation Output) Generate(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(TabularRecordAttribute).Assembly.Location));
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create("GeneratorFixture",
            [CSharpSyntaxTree.ParseText(source, parseOptions)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new RecordGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    [Fact]
    public void GeneratesDirectInitializerAndStaticInterfaceForRequiredInitAndEscapedIdentifiers()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            [TabularRecord] public partial class Model {
                public required string @class { get; init; }
                [Column("a\"b")] public int Value { get; set; }
            }
            public static class Query {
                public static CsvSource<T> Read<T>(string path) where T : class, ITabularRecord<T> => TabularFile.Csv<T>(path);
                public static CsvSource<Model> ReadModel(string path) => Read<Model>(path);
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        var code = Assert.Single(result.GeneratedTrees).GetText().ToString();
        Assert.Contains("new global::Model {", code);
        Assert.Contains("@class = row.ReadString", code);
        Assert.Contains("ITabularRecord<global::Model>", code);
        Assert.DoesNotContain("ModuleInitializer", code);
        Assert.DoesNotContain("Reflection", code);
    }

    [Theory]
    [InlineData("[TabularRecord] public class Model { public int Value { get; set; } }", "LTF001")]
    [InlineData("[TabularRecord] internal partial class Model { public int Value { get; set; } }", "LTF001")]
    [InlineData("[TabularRecord] public partial class Model { public Model(int x) {} }", "LTF001")]
    [InlineData("[TabularRecord] public partial class Model<T> { }", "LTF001")]
    [InlineData("[TabularRecord] public partial class Model { [Ignore] public required string Value { get; init; } }", "LTF002")]
    [InlineData("[TabularRecord] public partial class Model { [Column(\"x\")] public int Value { get; } }", "LTF002")]
    [InlineData("[TabularRecord] public partial class Model { [Column(\" \")] public int Value { get; set; } }", "LTF002")]
    [InlineData("[TabularRecord] public partial class Model { public System.DateTime Value { get; set; } }", "LTF003")]
    [InlineData("[TabularRecord] public partial class Model { [Column(\"x\"), Ignore] public int Value { get; set; } }", "LTF004")]
    [InlineData("[TabularRecord] public partial class Model { [Column(\"x\")] public int A { get; set; } [Column(\"x\")] public int B { get; set; } }", "LTF004")]
    [InlineData("[TabularRecord] public partial class Model { public required int Value; }", "LTF002")]
    public void ReportsUnsupportedShapes(string declaration, string id)
    {
        var (result, _) = Generate("using LinqToFile; " + declaration);
        Assert.Contains(result.Diagnostics, d => d.Id == id && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UnmarkedModelCannotBeUsedAsSource()
    {
        var (_, output) = Generate("""
            using LinqToFile;
            public class Model { public int Value { get; set; } }
            public static class Query {
                public static object Read(string path) => TabularFile.Csv<Model>(path);
            }
            """);
        Assert.Contains(output.GetDiagnostics(), d => d.Id == "CS0311");
    }

    [Fact]
    public void SupportsPublicNestedPartialModels()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            namespace Example;
            public static partial class Container {
                [TabularRecord] public sealed partial class Model { public required string Name { get; init; } }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InheritsColumnAndRejectsCrossKindOverrideConflict()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            public class Base { [Column("number")] public virtual int Value { get; set; } }
            [TabularRecord] public partial class Model : Base { public override int Value { get; set; } }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("\"number\"", Assert.Single(result.GeneratedTrees).GetText().ToString());
        var (conflict, _) = Generate("""
            using LinqToFile;
            public class Base { [Column("number")] public virtual int Value { get; set; } }
            [TabularRecord] public partial class Model : Base { [Ignore] public override int Value { get; set; } }
            """);
        Assert.Contains(conflict.Diagnostics, d => d.Id == "LTF004");
    }
}
