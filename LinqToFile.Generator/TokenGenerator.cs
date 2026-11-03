using System;
using Microsoft.CodeAnalysis;

namespace LinqToFile.Generator;

public sealed partial class RecordGenerator
{
    // Build-time metadata only. Runtime mapping embeds the selected literals and enum values.
    private sealed class TokenMapping
    {
        internal string? TrueToken;
        internal string? FalseToken;
        internal StringComparison BooleanComparison = StringComparison.OrdinalIgnoreCase;
        internal string? NullToken;
        internal StringComparison NullComparison = StringComparison.Ordinal;
    }

    private static string? ReadTokens(IPropertySymbol property, ITypeSymbol valueType, bool nullableValue,
        AttributeData? boolean, AttributeData? nullToken, out TokenMapping? mapping)
    {
        mapping = null;
        if (boolean == null && nullToken == null) return null;
        var result = new TokenMapping();
        if (boolean != null)
        {
            if (valueType.SpecialType != SpecialType.System_Boolean) return "BooleanTokens supports only bool and bool?.";
            result.TrueToken = boolean.ConstructorArguments.Length > 0 ? boolean.ConstructorArguments[0].Value as string : null;
            result.FalseToken = boolean.ConstructorArguments.Length > 1 ? boolean.ConstructorArguments[1].Value as string : null;
            if (!ValidToken(result.TrueToken) || !ValidToken(result.FalseToken)) return "Tokens must be nonempty strings without leading or trailing whitespace.";
            if (!ReadComparison(boolean, ref result.BooleanComparison)) return "Use StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase.";
            if (string.Equals(result.TrueToken, result.FalseToken, result.BooleanComparison)) return "True and false tokens must differ under the selected comparison.";
        }
        if (nullToken != null)
        {
            bool nullableString = property.Type.SpecialType == SpecialType.System_String && property.NullableAnnotation == NullableAnnotation.Annotated;
            if (!nullableString && (!nullableValue || ReadExpression(valueType, 0, property.Name) == null))
                return "NullToken requires string? or a supported nullable value type, including nullable enums.";
            result.NullToken = nullToken.ConstructorArguments.Length > 0 ? nullToken.ConstructorArguments[0].Value as string : null;
            if (!ValidToken(result.NullToken)) return "Tokens must be nonempty strings without leading or trailing whitespace.";
            if (!ReadComparison(nullToken, ref result.NullComparison)) return "Use StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase.";
        }
        mapping = result;
        return null;
    }

    private static bool ValidToken(string? token) => !string.IsNullOrEmpty(token) &&
        !char.IsWhiteSpace(token![0]) && !char.IsWhiteSpace(token[token.Length - 1]);

    private static bool ReadComparison(AttributeData attribute, ref StringComparison comparison)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument is { Key: "Comparison", Value.Value: int value }) comparison = (StringComparison)value;
        return comparison is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase;
    }

    private static string ComparisonExpression(StringComparison comparison) => comparison == StringComparison.Ordinal
        ? "global::System.StringComparison.Ordinal" : "global::System.StringComparison.OrdinalIgnoreCase";
}
