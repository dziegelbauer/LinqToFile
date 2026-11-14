using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    [Fact]
    public void ConstructorArgumentsUseParameterOrderAndKeepPropertyMetadata()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            [TabularRecord] public partial class Model {
                [Column("identifier")] public int Id { get; }
                public string Name { get; }
                public required int Other { get; init; }
                public Model() => Name = "wrong";
                [TabularConstructor] public Model(string NAME, int id) { Name = NAME; Id = id; }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        string code = Assert.Single(result.GeneratedTrees).GetText().ToString();
        Assert.Contains("new global::Model(value_1, value_0)", code);
        Assert.Contains("@Other = value_2", code);
        Assert.DoesNotContain("@Id =", code);
        Assert.Contains("\"identifier\"", code);
        Assert.DoesNotContain("Reflection", code);
    }

    [Theory]
    [InlineData("[TabularConstructor] private Model(int value) {} public int Value { get; } ")]
    [InlineData("[TabularConstructor] public Model(int value) {} [TabularConstructor] public Model() {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(int missing) {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(long value) {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(string? value) {} public string Value { get; } = \"\";")]
    [InlineData("[TabularConstructor] public Model(int value) {} [Ignore] public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(int value) {} public int Value { get; } public int VALUE { get; }")]
    [InlineData("[TabularConstructor] public Model(int value, int VALUE) {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(ref int value) {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(in int value) {} public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(out int value) { value = 0; } public int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(params int[] value) {} public int[] Value { get; }")]
    [InlineData("[TabularConstructor] static Model() {} public int Value { get; set; }")]
    [InlineData("[TabularConstructor] public Model(int value) {} public int Value { private get; set; }")]
    [InlineData("[TabularConstructor] public Model(int value) {} public static int Value { get; }")]
    [InlineData("[TabularConstructor] public Model(int value) {} public required int Value { get; init; }")]
    public void RejectsInvalidConstructorBindings(string members)
    {
        var (result, _) = Generate("using LinqToFile; [TabularRecord] public partial class Model { " + members + " }");
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF009");
        Assert.Empty(result.GeneratedTrees);
    }

    [Theory]
    [InlineData("[TabularConstructor] public Model() {} public int Value { get; init; }")]
    [InlineData("[TabularConstructor] public Model(int value = 42) {} public int Value { get; }")]
    [InlineData("[TabularConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Model(int value) { Value = value; } public required int Value { get; init; }")]
    [InlineData("[TabularConstructor] public Model(int @class) {} public int @class { get; }")]
    public void SupportsExplicitZeroArgumentsOptionalParametersAndRequiredMembers(string members)
    {
        var (result, output) = Generate("using LinqToFile; [TabularRecord] public partial class Model { " + members + " }");
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void BindsInheritedPropertiesAndKeepsOverrideAnnotations()
    {
        var (result, output) = Generate("""
            using LinqToFile;
            public class Base<T> { [Column("number")] public virtual T Value { get; } = default!; }
            [TabularRecord] public partial class Model : Base<int> {
                public override int Value { get; }
                [TabularConstructor] public Model(int value) { Value = value; }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("\"number\"", Assert.Single(result.GeneratedTrees).GetText().ToString());
    }

    [Fact]
    public void PrimaryConstructorsRemainUnsupported()
    {
        var (result, _) = Generate("using LinqToFile; [TabularRecord] public partial class Model(int value) { public Model() : this(0) {} public int Value { get; } = value; }");
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF001");
    }
}
