using Microsoft.CodeAnalysis;
using Xunit;

namespace LinqToFile.Tests;

public sealed partial class GeneratorTests
{
    private const string ConverterFixture = """
        using System;
        using LinqToFile;
        public readonly struct Converter<T> : IColumnConverter<T> {
            static bool IColumnConverter<T>.TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out T value) { value = default!; return true; }
            static bool IColumnConverter<T>.TryFormat(T value, Span<char> destination, ColumnConversionContext context, out int written) { written = 0; return true; }
        }
        """;

    [Theory]
    [InlineData("int")]
    [InlineData("int?")]
    [InlineData("string")]
    [InlineData("DateTime")]
    [InlineData("object")]
    public void GeneratesStaticConverterDispatchForTheCompletePropertyType(string type)
    {
        var (result, output) = Generate(ConverterFixture + $$"""
            [TabularRecord] public partial class Model {
                [ColumnConverter(typeof(Converter<{{type}}>))] public {{type}} Value { get; init; } = default!;
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        var code = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("row.ReadConverted<", code);
        Assert.Contains("value.SetConverted<", code);
        Assert.DoesNotContain("row.IsEmpty", code);
        Assert.DoesNotContain("field.HasValue", code);
        Assert.DoesNotContain("new global::Converter", code);
    }

    [Theory]
    [InlineData("[ColumnConverter(typeof(Converter<string>))] public int Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(Converter<int>))] public int? Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(Converter<string>))] public string? Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(Converter<>))] public int Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(IColumnConverter<int>))] public int Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(string))] public int Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(null)] public int Value { get; set; }", "LTF005")]
    [InlineData("[ColumnConverter(typeof(Converter<int>)), Ignore] public int Value { get; set; }", "LTF004")]
    [InlineData("[ColumnConverter(typeof(Converter<int>))] public int Value { get; }", "LTF002")]
    [InlineData("[ColumnConverter(typeof(Converter<int>))] public static int Value { get; set; }", "LTF002")]
    public void ReportsInvalidConverterMappings(string property, string diagnostic)
    {
        var (result, _) = Generate(ConverterFixture + "[TabularRecord] public partial class Model { " + property + " }");
        Assert.Contains(result.Diagnostics, d => d.Id == diagnostic);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void InheritsNearestConverterAndRejectsIgnoreConflicts()
    {
        const string source = """
            public class Base {
                [Column("amount"), ColumnConverter(typeof(Converter<int>))] public virtual int Value { get; set; }
            }
            [TabularRecord] public partial class Model : Base { public override int Value { get; set; } }
            """;
        var (result, output) = Generate(ConverterFixture + source);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("row.ReadConverted<int, global::Converter<int>>", Assert.Single(result.GeneratedTrees).ToString());
        var (conflict, _) = Generate(ConverterFixture + source.Replace("public override", "[Ignore] public override"));
        Assert.Contains(conflict.Diagnostics, d => d.Id == "LTF004");
    }

    [Fact]
    public void RejectsConvertersInaccessibleToTheGeneratedMapper()
    {
        var (result, _) = Generate("""
            using System; using LinqToFile;
            [TabularRecord] public partial class Model {
                [ColumnConverter(typeof(Converter))] public int Value { get; set; }
                private struct Converter : IColumnConverter<int> {
                    public static bool TryParse(ReadOnlySpan<char> s, ColumnConversionContext c, out int v) { v=0; return true; }
                    public static bool TryFormat(int v, Span<char> s, ColumnConversionContext c, out int n) { n=0; return true; }
                }
            }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF005");
    }

    [Fact]
    public void SupportsClassConvertersWithoutPublicConstructorsAndSetterOnlyModels()
    {
        var fixture = ConverterFixture.Replace("public readonly struct Converter<T>", "public sealed class Converter<T>")
            .Replace("static bool IColumnConverter<T>.TryParse", "private Converter() {} static bool IColumnConverter<T>.TryParse");
        var (result, output) = Generate(fixture + """
            [TabularRecord] public partial class Model {
                [ColumnConverter(typeof(Converter<int>))] public int Value { set {} }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning);
        string code = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("row.ReadConverted<", code);
        Assert.Contains("CanWrite => false", code);
        Assert.DoesNotContain("record.@Value", code);
    }

    [Theory]
    [InlineData("public abstract class Converter<T>")]
    [InlineData("file class Converter<T>")]
    public void RejectsAbstractAndFileLocalConverters(string declaration)
    {
        var (result, _) = Generate(ConverterFixture.Replace("public readonly struct Converter<T>", declaration) + """
            [TabularRecord] public partial class Model {
                [ColumnConverter(typeof(Converter<int>))] public int Value { get; set; }
            }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF005");
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void RejectsConvertersNestedInFileLocalTypes()
    {
        var fixture = ConverterFixture.Replace("public readonly struct Converter<T>", "file class Holder { public readonly struct Converter<T>") + "}";
        var (result, _) = Generate(fixture + """
            [TabularRecord] public partial class Model {
                [ColumnConverter(typeof(Holder.Converter<int>))] public int Value { get; set; }
            }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "LTF005");
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void DerivedConverterReplacesInheritedConverter()
    {
        var (result, output) = Generate(ConverterFixture + """
            public readonly struct Other : IColumnConverter<int> {
                public static bool TryParse(ReadOnlySpan<char> s, ColumnConversionContext c, out int v) { v=0; return true; }
                public static bool TryFormat(int v, Span<char> s, ColumnConversionContext c, out int n) { n=0; return true; }
            }
            public class Base {
                [ColumnConverter(typeof(Converter<int>))] public virtual int Value { get; set; }
            }
            [TabularRecord] public partial class Model : Base {
                [ColumnConverter(typeof(Other))] public override int Value { get; set; }
            }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        string code = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("row.ReadConverted<int, global::Other>", code);
        Assert.DoesNotContain("global::Converter<int>", code);
    }
}
