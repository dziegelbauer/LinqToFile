using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LinqToFile.Mapping;
using Microsoft.CodeAnalysis;

namespace LinqToFile.Generator;

public sealed partial class RecordGenerator
{
    private static TemporalKind? GetTemporalKind(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            type = named.TypeArguments[0];
        switch (type.ToDisplayString())
        {
            case "System.DateTime": return TemporalKind.DateTime;
            case "System.DateTimeOffset": return TemporalKind.DateTimeOffset;
            case "System.DateOnly": return TemporalKind.DateOnly;
            case "System.TimeOnly": return TemporalKind.TimeOnly;
            default: return null;
        }
    }

    private static string? ValidateTemporalFormat(ITypeSymbol type, string? format)
    {
        var kind = GetTemporalKind(type);
        if (kind == null) return "TemporalFormat supports DateTime, DateTimeOffset, DateOnly, and TimeOnly, including nullable forms.";
        string? error = TemporalFormatRules.GetError(format, kind.Value, CultureInfo.InvariantCulture);
        if (error != null) return error;
        // The analyzer targets netstandard2.0. Date/time portions use the same BCL custom
        // syntax; expand the DateOnly/TimeOnly fixed standards for this syntax probe.
        string pattern = format!;
        if (pattern.Length == 1 && (kind == TemporalKind.DateOnly || kind == TemporalKind.TimeOnly))
        {
            if (pattern == "O" || pattern == "o") pattern = kind == TemporalKind.DateOnly ? "yyyy'-'MM'-'dd" : "HH':'mm':'ss.fffffff";
            if (pattern == "R" || pattern == "r") pattern = kind == TemporalKind.DateOnly ? "ddd, dd MMM yyyy" : "HH':'mm':'ss";
        }
        var sample = new DateTime(2004, 11, 23, 13, 45, 56, DateTimeKind.Unspecified).AddTicks(1234567);
        try
        {
            if (kind == TemporalKind.DateTimeOffset)
            {
                var offset = new DateTimeOffset(sample, TimeSpan.FromMinutes(345));
                string text = offset.ToString(pattern, CultureInfo.InvariantCulture);
                if (DateTimeOffset.TryParseExact(text, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed.Offset == offset.Offset)
                    return null;
            }
            else
            {
                string text = sample.ToString(pattern, CultureInfo.InvariantCulture);
                if (DateTime.TryParseExact(text, pattern, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed) && parsed.Kind == DateTimeKind.Unspecified)
                    return null;
            }
        }
        catch (FormatException) { }
        return "Use a valid .NET format that supports both exact parsing and formatting without timezone conversion.";
    }

    private static void EmitTemporalCultureValidation(StringBuilder source, List<IPropertySymbol> properties, List<string?> formats)
    {
        if (formats.All(f => f == null)) return;
        source.AppendLine("public override void ValidateCulture(global::System.Globalization.CultureInfo culture) {");
        for (int i = 0; i < formats.Count; i++)
            if (formats[i] != null)
                source.Append("global::LinqToFile.Infrastructure.TemporalFormats.Validate").Append(GetTemporalKind(properties[i].Type))
                    .Append("(").Append(Literal(formats[i]!)).AppendLine(", culture);");
        source.AppendLine("}");
    }
}
