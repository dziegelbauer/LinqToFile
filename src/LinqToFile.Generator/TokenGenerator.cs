using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LinqToFile.Generator;

public sealed partial class RecordGenerator
{
    // Build-time metadata only. Runtime mapping embeds the selected literals and enum values.
    private sealed class TokenMapping
    {
        internal string? TrueToken;
        internal string? FalseToken;
        internal readonly List<string> TrueAliases = new List<string>();
        internal readonly List<string> FalseAliases = new List<string>();
        internal StringComparison BooleanComparison = StringComparison.OrdinalIgnoreCase;
        internal string? NullToken;
        internal readonly List<string> NullAliases = new List<string>();
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
            if (!ReadAliases(boolean, "TrueAliases", result.TrueAliases) || !ReadAliases(boolean, "FalseAliases", result.FalseAliases))
                return "Alias arrays must be non-null and contain nonempty strings without leading or trailing whitespace.";
            var trueTokens = new List<string>(result.TrueAliases) { result.TrueToken! };
            var falseTokens = new List<string>(result.FalseAliases) { result.FalseToken! };
            foreach (var trueToken in trueTokens)
                foreach (var falseToken in falseTokens)
                    if (string.Equals(trueToken, falseToken, result.BooleanComparison))
                        return "True and false tokens and aliases must differ under the selected comparison.";
        }
        if (nullToken != null)
        {
            bool nullableString = property.Type.SpecialType == SpecialType.System_String && property.NullableAnnotation == NullableAnnotation.Annotated;
            if (!nullableString && (!nullableValue || ReadExpression(valueType, 0, property.Name) == null))
                return "NullToken requires string? or a supported nullable value type, including nullable enums.";
            result.NullToken = nullToken.ConstructorArguments.Length > 0 ? nullToken.ConstructorArguments[0].Value as string : null;
            if (!ValidToken(result.NullToken)) return "Tokens must be nonempty strings without leading or trailing whitespace.";
            if (!ReadComparison(nullToken, ref result.NullComparison)) return "Use StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase.";
            if (!ReadAliases(nullToken, "Aliases", result.NullAliases))
                return "Alias arrays must be non-null and contain nonempty strings without leading or trailing whitespace.";
        }
        mapping = result;
        return null;
    }

    private static bool ValidToken(string? token) => !string.IsNullOrEmpty(token) &&
        !char.IsWhiteSpace(token![0]) && !char.IsWhiteSpace(token[token.Length - 1]);

    private static bool ReadAliases(AttributeData attribute, string name, List<string> aliases)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != name) continue;
            if (argument.Value.IsNull) return false;
            foreach (var item in argument.Value.Values)
            {
                if (item.Value is not string token || !ValidToken(token)) return false;
                aliases.Add(token);
            }
        }
        return true;
    }

    private static string TextTokenExpression(int index, string token, StringComparison comparison) =>
        "row.IsTextToken(ordinals[" + index + "], " + Literal(token) + ", " + ComparisonExpression(comparison) + ")";

    private static string ReadBooleanExpression(int index, string member, TokenMapping tokens)
    {
        string expression = "row.ReadBooleanTokens(ordinals[" + index + "], " + Literal(member) + ", " +
            Literal(tokens.TrueToken!) + ", " + Literal(tokens.FalseToken!) + ", " + ComparisonExpression(tokens.BooleanComparison) + ")";
        foreach (var alias in tokens.FalseAliases)
            expression = TextTokenExpression(index, alias, tokens.BooleanComparison) + " ? false : " + expression;
        foreach (var alias in tokens.TrueAliases)
            expression = TextTokenExpression(index, alias, tokens.BooleanComparison) + " ? true : " + expression;
        return expression;
    }

    private static bool ReadComparison(AttributeData attribute, ref StringComparison comparison)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument is { Key: "Comparison", Value.Value: int value }) comparison = (StringComparison)value;
        return comparison is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase;
    }

    private static string ComparisonExpression(StringComparison comparison) => comparison == StringComparison.Ordinal
        ? "global::System.StringComparison.Ordinal" : "global::System.StringComparison.OrdinalIgnoreCase";
}
