using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LinqToFile.Generator;

[Generator(LanguageNames.CSharp)]
public sealed partial class RecordGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidModel = Rule("LTF001", "Unsupported record model");
    private static readonly DiagnosticDescriptor InvalidMember = Rule("LTF002", "Unsupported record member");
    private static readonly DiagnosticDescriptor InvalidType = Rule("LTF003", "Unsupported property type");
    private static readonly DiagnosticDescriptor Conflict = Rule("LTF004", "Conflicting column mapping");
    private static readonly DiagnosticDescriptor InvalidConverter = Rule("LTF005", "Unsupported column converter");
    private static readonly DiagnosticDescriptor InvalidTemporalFormat = Rule("LTF006", "Unsupported temporal format");
    private static readonly DiagnosticDescriptor InvalidTokens = Rule("LTF007", "Unsupported column tokens");
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static DiagnosticDescriptor Rule(string id, string title) =>
        new(id, title, "{0}", "LinqToFile", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
            "LinqToFile.TabularRecordAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);
        context.RegisterSourceOutput(models.Combine(context.CompilationProvider),
            static (output, pair) => Emit(output, pair.Left, pair.Right));
    }

    private static void Emit(SourceProductionContext context, INamedTypeSymbol model, Compilation compilation)
    {
        bool failed = false;
        void Error(DiagnosticDescriptor rule, ISymbol symbol, string message)
        {
            failed = true;
            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource) ??
                model.Locations.FirstOrDefault(l => l.IsInSource);
            context.ReportDiagnostic(Diagnostic.Create(rule, location, symbol.Name + ": " + message));
        }

        bool accessible = true;
        for (var owner = model; owner != null; owner = owner.ContainingType)
            accessible &= owner.DeclaredAccessibility == Accessibility.Public && owner.Arity == 0 &&
                owner.DeclaringSyntaxReferences.All(r =>
                    r.GetSyntax() is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        if (model.TypeKind != TypeKind.Class || model.IsRecord || model.IsAbstract || model.IsStatic ||
            !accessible || !model.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0))
        {
            Error(InvalidModel, model, "Use a public, partial, non-generic, non-abstract class with a public parameterless constructor. Containing types must be public, partial, and non-generic. Record classes are not supported yet.");
            return;
        }

        var properties = new List<IPropertySymbol>();
        var columns = new List<string>();
        var readers = new List<string>();
        var nullable = new List<bool>();
        var converters = new List<string?>();
        var formats = new List<string?>();
        var tokens = new List<TokenMapping?>();
        var enums = new List<KeyValuePair<int, INamedTypeSymbol>>();
        var seen = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        var columnSet = new HashSet<string>(StringComparer.Ordinal);
        for (var type = model; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsRequired))
                Error(InvalidMember, field, "Required fields are not supported. Use a property.");

            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                AttributeData? column = null;
                AttributeData? converter = null;
                AttributeData? temporal = null;
                AttributeData? booleanTokens = null;
                AttributeData? nullToken = null;
                bool ignored = false;
                for (var declaration = property; declaration != null; declaration = declaration.OverriddenProperty)
                {
                    foreach (var attribute in declaration.GetAttributes())
                    {
                        switch (attribute.AttributeClass?.ToDisplayString())
                        {
                            case "LinqToFile.ColumnAttribute" when column is null: column = attribute; break;
                            case "LinqToFile.ColumnConverterAttribute" when converter is null: converter = attribute; break;
                            case "LinqToFile.TemporalFormatAttribute" when temporal is null: temporal = attribute; break;
                            case "LinqToFile.BooleanTokensAttribute" when booleanTokens is null: booleanTokens = attribute; break;
                            case "LinqToFile.NullTokenAttribute" when nullToken is null: nullToken = attribute; break;
                            case "LinqToFile.IgnoreAttribute": ignored = true; break;
                        }
                    }
                }

                // The most-derived override already includes effective annotations from the chain.
                if (seen.TryGetValue(property.Name, out var derived) &&
                    derived is IPropertySymbol derivedProperty && Overrides(derivedProperty, property))
                    continue;

                if ((ignored && (column != null || converter != null || temporal != null || booleanTokens != null || nullToken != null)) ||
                    (converter != null && (temporal != null || booleanTokens != null || nullToken != null)))
                {
                    Error(Conflict, property, "Ignore cannot combine with mapping annotations; ColumnConverter cannot combine with TemporalFormat or token annotations, including inherited annotations.");
                    continue;
                }
                if (ignored)
                {
                    if (property.IsRequired) Error(InvalidMember, property, "A required property cannot be ignored.");
                    continue;
                }
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public ||
                    property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    if (column != null || converter != null || temporal != null || booleanTokens != null || nullToken != null || property.IsRequired)
                        Error(InvalidMember, property, "Mapped and required properties must be instance properties with a public setter or public init accessor, and cannot be indexers.");
                    continue;
                }
                if (seen.ContainsKey(property.Name))
                {
                    Error(Conflict, property, "A derived member hides a mapped base property.");
                    continue;
                }

                var header = column == null ? property.Name : column.ConstructorArguments.FirstOrDefault().Value as string;
                if (string.IsNullOrWhiteSpace(header))
                {
                    Error(InvalidMember, property, "Column names cannot be null, empty, or whitespace-only.");
                    continue;
                }
                if (!columnSet.Add(header!)) Error(Conflict, property, "Multiple properties map to the same column name.");

                var valueType = property.Type;
                bool isNullable = valueType is INamedTypeSymbol n &&
                    n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
                if (isNullable) valueType = ((INamedTypeSymbol)valueType).TypeArguments[0];
                int index = properties.Count;
                string? tokenError = ReadTokens(property, valueType, isNullable, booleanTokens, nullToken, out var tokenMapping);
                if (tokenError != null)
                {
                    Error(InvalidTokens, property, tokenError);
                    continue;
                }
                string? format = null;
                if (temporal != null)
                {
                    format = temporal.ConstructorArguments.FirstOrDefault().Value as string;
                    string? formatError = ValidateTemporalFormat(valueType, format);
                    if (formatError != null)
                    {
                        Error(InvalidTemporalFormat, property, formatError);
                        continue;
                    }
                }
                string? converterName = null;
                if (converter != null)
                {
                    var converterType = converter.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                    var contract = compilation.GetTypeByMetadataName("LinqToFile.IColumnConverter`1");
                    if (converterType == null || converterType.IsUnboundGenericType || converterType.IsAbstract || ContainsFileLocalType(converterType) ||
                        converterType.IsRefLikeType || (converterType.TypeKind != TypeKind.Class && converterType.TypeKind != TypeKind.Struct) ||
                        !compilation.IsSymbolAccessibleWithin(converterType, model.ContainingAssembly) ||
                        !converterType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, contract) &&
                            SymbolEqualityComparer.IncludeNullability.Equals(i.TypeArguments[0], property.Type)))
                    {
                        Error(InvalidConverter, property, "Use an accessible, closed, non-abstract class or struct implementing IColumnConverter<T> for the complete property type, including its nullability.");
                        continue;
                    }
                    converterName = converterType.ToDisplayString(TypeFormat);
                }
                var reader = converterName == null ? ReadExpression(valueType, index, property.Name) :
                    "row.ReadConverted<" + property.Type.ToDisplayString(TypeFormat) + ", " + converterName +
                    ">(ordinals[" + index + "], " + Literal(property.Name) + ")";
                if (format != null) reader = "row.Read" + GetTemporalKind(valueType) + "Exact(ordinals[" + index + "], " + Literal(property.Name) + ", " + Literal(format) + ")";
                if (tokenMapping?.TrueToken != null) reader = "row.ReadBooleanTokens(ordinals[" + index + "], " + Literal(property.Name) + ", " +
                    Literal(tokenMapping.TrueToken) + ", " + Literal(tokenMapping.FalseToken!) + ", " + ComparisonExpression(tokenMapping.BooleanComparison) + ")";
                if (reader == null)
                {
                    Error(InvalidType, property, "Use a supported scalar type or enum, or annotate the property with ColumnConverter.");
                    continue;
                }
                if (converterName == null && valueType.TypeKind == TypeKind.Enum)
                    enums.Add(new KeyValuePair<int, INamedTypeSymbol>(index, (INamedTypeSymbol)valueType));
                properties.Add(property);
                columns.Add(header!);
                readers.Add(reader);
                nullable.Add(isNullable);
                converters.Add(converterName);
                formats.Add(format);
                tokens.Add(tokenMapping);
            }

            // Fields and methods can also hide an inherited property from an object initializer.
            foreach (var member in type.GetMembers())
                if (!seen.ContainsKey(member.Name)) seen.Add(member.Name, member);
        }
        if (failed) return;

        string modelName = model.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string id;
        using (var hash = SHA256.Create())
            id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(modelName))).Replace("-", "");
        string mapper = "Mapper_" + id;
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine("using global::System;");
        source.AppendLine("namespace LinqToFile.Generated {");
        source.Append("internal sealed class ").Append(mapper)
            .Append(" : global::LinqToFile.Infrastructure.RecordMapper<").Append(modelName).AppendLine("> {");
        source.Append("internal static readonly ").Append(mapper).Append(" Instance = new ").Append(mapper).AppendLine("();");
        source.Append("private static readonly string[] names = new string[] {");
        foreach (var column in columns) source.Append(Literal(column)).Append(",");
        source.AppendLine("};");
        source.AppendLine("public override global::System.ReadOnlySpan<string> Columns => names;");
        bool sharedNames = columns.SequenceEqual(properties.Select(p => p.Name), StringComparer.Ordinal);
        if (!sharedNames)
        {
            source.Append("private static readonly string[] members = new string[] {");
            foreach (var property in properties) source.Append(Literal(property.Name)).Append(",");
            source.AppendLine("};");
        }
        source.Append("public override global::System.ReadOnlySpan<string> Members => ")
            .AppendLine(sharedNames ? "names;" : "members;");
        source.Append("public override ").Append(modelName)
            .AppendLine(" Read(global::LinqToFile.Infrastructure.TabularRow row, global::System.ReadOnlySpan<int> ordinals) =>");
        source.Append("new ").Append(modelName).AppendLine(" {");
        for (int i = 0; i < properties.Count; i++)
        {
            source.Append("@").Append(properties[i].Name).Append(" = ");
            if (tokens[i]?.NullToken is { } nullToken)
                source.Append("row.IsNullToken(ordinals[").Append(i).Append("], ").Append(Literal(nullToken)).Append(", ")
                    .Append(ComparisonExpression(tokens[i]!.NullComparison)).Append(") ? null : ");
            if (nullable[i] && converters[i] == null) source.Append("row.IsEmpty(ordinals[").Append(i).Append("]) ? null : ");
            source.Append(readers[i]).AppendLine(",");
        }
        source.AppendLine("};");
        EmitTemporalCultureValidation(source, properties, formats);
        EmitWriter(source, model, modelName, properties, nullable, enums, converters, formats, tokens);
        foreach (var item in enums) EmitEnum(source, item.Key, item.Value);
        source.AppendLine("} }");

        if (!model.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(model.ContainingNamespace.ToDisplayString()).AppendLine(" {");
        var containers = new Stack<INamedTypeSymbol>();
        for (var owner = model.ContainingType; owner != null; owner = owner.ContainingType) containers.Push(owner);
        foreach (var owner in containers)
        {
            if (owner.IsReadOnly) source.Append("readonly ");
            if (owner.IsRefLikeType) source.Append("ref ");
            var kind = owner.IsRecord ? (owner.TypeKind == TypeKind.Struct ? "record struct" : "record class")
                : owner.TypeKind == TypeKind.Struct ? "struct" : owner.TypeKind == TypeKind.Interface ? "interface" : "class";
            source.Append("partial ").Append(kind).Append(" @").Append(owner.Name).AppendLine(" {");
        }
        source.Append("partial class @").Append(model.Name)
            .Append(" : global::LinqToFile.ITabularRecord<").Append(modelName).AppendLine("> {");
        source.Append("static global::LinqToFile.Infrastructure.RecordMapper<").Append(modelName)
            .Append("> global::LinqToFile.ITabularRecord<").Append(modelName)
            .Append(">.Mapper => global::LinqToFile.Generated.").Append(mapper).AppendLine(".Instance;");
        source.AppendLine("}");
        foreach (var _ in containers) source.AppendLine("}");
        if (!model.ContainingNamespace.IsGlobalNamespace) source.AppendLine("}");
        context.AddSource(mapper + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static bool Overrides(IPropertySymbol derived, IPropertySymbol ancestor)
    {
        for (var current = derived.OverriddenProperty; current != null; current = current.OverriddenProperty)
            if (SymbolEqualityComparer.Default.Equals(current, ancestor)) return true;
        return false;
    }

    private static bool ContainsFileLocalType(ITypeSymbol type)
    {
        while (type is IArrayTypeSymbol array) type = array.ElementType;
        if (type is not INamedTypeSymbol named) return false;
        return named.IsFileLocal || (named.ContainingType != null && ContainsFileLocalType(named.ContainingType)) ||
            named.TypeArguments.Any(ContainsFileLocalType);
    }

    private static bool CanReadProperty(IPropertySymbol property, INamedTypeSymbol model)
    {
        var getter = property.GetMethod;
        return getter != null && (getter.DeclaredAccessibility == Accessibility.Public ||
            (SymbolEqualityComparer.Default.Equals(getter.ContainingAssembly, model.ContainingAssembly) &&
             getter.DeclaredAccessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal));
    }

    private static void EmitWriter(StringBuilder source, INamedTypeSymbol model, string modelName,
        List<IPropertySymbol> properties, List<bool> nullable, List<KeyValuePair<int, INamedTypeSymbol>> enums, List<string?> converters, List<string?> formats, List<TokenMapping?> tokens)
    {
        int unreadable = properties.FindIndex(p => !CanReadProperty(p, model));
        source.Append("public override bool CanWrite => ").Append(unreadable < 0 ? "true" : "false").AppendLine(";");
        source.Append("public override int UnreadableMemberIndex => ").Append(unreadable).AppendLine(";");
        source.Append("public override void WriteField(").Append(modelName)
            .AppendLine(" record, int ordinal, global::LinqToFile.Infrastructure.TabularValue value) {");
        source.AppendLine("switch (ordinal) {");
        for (int i = 0; i < properties.Count; i++)
        {
            source.Append("case ").Append(i).AppendLine(": {");
            if (!CanReadProperty(properties[i], model))
            {
                source.AppendLine("throw new global::System.NotSupportedException(); }");
                continue;
            }
            source.Append("var field = record.@").Append(properties[i].Name).AppendLine(";");
            if (converters[i] != null)
            {
                source.Append("value.SetConverted<").Append(properties[i].Type.ToDisplayString(TypeFormat)).Append(", ")
                    .Append(converters[i]).AppendLine(">(field);");
                source.AppendLine("return; }");
                continue;
            }
            string nullText = tokens[i]?.NullToken is { } nullToken ? Literal(nullToken) : "null";
            if (nullable[i]) source.Append("if (!field.HasValue) { value.SetText(").Append(nullText).AppendLine("); return; }");
            else if (tokens[i]?.NullToken != null) source.Append("if (field is null) { value.SetText(").Append(nullText).AppendLine("); return; }");
            string field = nullable[i] ? "field.Value" : "field";
            ITypeSymbol type = nullable[i] ? ((INamedTypeSymbol)properties[i].Type).TypeArguments[0] : properties[i].Type;
            if (tokens[i]?.TrueToken is { } trueToken)
                source.Append("value.SetText(").Append(field).Append(" ? ").Append(Literal(trueToken)).Append(" : ").Append(Literal(tokens[i]!.FalseToken!)).AppendLine(");");
            else if (formats[i] != null)
                source.Append("value.Set(").Append(field).Append(", ").Append(Literal(formats[i]!)).AppendLine(");");
            else if (type.TypeKind == TypeKind.Enum)
                source.Append("WriteEnum_").Append(i).Append("(").Append(field).AppendLine(", value);");
            else if (type.SpecialType == SpecialType.System_String)
                source.Append("value.SetText(").Append(field).AppendLine(");");
            else
            {
                bool number = ReadExpression(type, i, "")!.Contains("ReadNumber<") || type.SpecialType == SpecialType.System_Decimal;
                bool floating = type.SpecialType == SpecialType.System_Single || type.SpecialType == SpecialType.System_Double || type.ToDisplayString() == "System.Half";
                source.Append(number ? "value.SetNumber(" : "value.Set(").Append(field);
                if (floating) source.Append(", \"R\"");
                source.AppendLine(");");
            }
            if (tokens[i]?.NullToken is { } token)
                source.Append("value.RejectNullToken(").Append(Literal(token)).Append(", ").Append(ComparisonExpression(tokens[i]!.NullComparison)).AppendLine(");");
            source.AppendLine("return; }");
        }
        source.AppendLine("default: throw new global::System.ArgumentOutOfRangeException(nameof(ordinal)); } }");
        foreach (var item in enums)
        {
            string name = item.Value.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var fields = item.Value.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            bool flags = item.Value.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
            source.Append("private static void WriteEnum_").Append(item.Key).Append("(").Append(name)
                .AppendLine(" field, global::LinqToFile.Infrastructure.TabularValue value) {");
            foreach (var f in fields)
                source.Append("if (field == ").Append(EnumConstant(name, f))
                    .Append(") { value.SetText(").Append(Literal(f.Name)).AppendLine("); return; }");
            if (flags)
            {
                source.Append(name).AppendLine(" covered = default;");
                foreach (var f in fields)
                {
                    string constant = EnumConstant(name, f);
                    source.Append("if (").Append(constant).Append(" != 0 && (field & ").Append(constant).Append(") == ")
                        .Append(constant).Append(" && (covered | ").Append(constant).AppendLine(") != covered) {");
                    source.AppendLine("if (covered != 0) value.AppendText(\", \");");
                    source.Append("value.AppendText(").Append(Literal(f.Name)).AppendLine(");");
                    source.Append("covered |= ").Append(constant).AppendLine("; }");
                }
                source.AppendLine("if (covered != 0 && covered == field) return;");
            }
            source.AppendLine("throw new global::System.FormatException(); }");
        }
    }

    private static string? ReadExpression(ITypeSymbol type, int index, string member)
    {
        string arguments = "ordinals[" + index + "], " + Literal(member);
        if (type.TypeKind == TypeKind.Enum) return "ReadEnum_" + index + "(row, " + arguments + ")";
        string? method = null;
        string? styles = null;
        switch (type.SpecialType)
        {
            case SpecialType.System_String: method = "ReadString"; break;
            case SpecialType.System_Char: method = "ReadChar"; break;
            case SpecialType.System_Boolean: method = "ReadBoolean"; break;
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                styles = "Integer"; break;
            case SpecialType.System_Single:
            case SpecialType.System_Double:
                styles = "Float"; break;
            case SpecialType.System_Decimal:
                method = "ReadDecimal"; break;
        }
        switch (type.ToDisplayString())
        {
            case "System.Int128":
            case "System.UInt128": styles = "Integer"; break;
            case "System.Half": styles = "Float"; break;
            case "System.Guid": method = "ReadGuid"; break;
            case "System.DateTime": method = "ReadDateTime"; break;
            case "System.DateTimeOffset": method = "ReadDateTimeOffset"; break;
            case "System.DateOnly": method = "ReadDateOnly"; break;
            case "System.TimeOnly": method = "ReadTimeOnly"; break;
            case "System.TimeSpan": method = "ReadTimeSpan"; break;
        }
        if (styles != null)
            return "row.ReadNumber<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(" +
                arguments + ", global::System.Globalization.NumberStyles." + styles + ")";
        return method == null ? null : "row." + method + "(" + arguments + ")";
    }

    private static void EmitEnum(StringBuilder source, int index, INamedTypeSymbol type)
    {
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string suffix = index.ToString();
        bool flags = type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
        source.Append("private static ").Append(name).Append(" ReadEnum_").Append(suffix)
            .AppendLine("(global::LinqToFile.Infrastructure.TabularRow row, int ordinal, string member) {");
        source.AppendLine("var text = row.GetTextField(ordinal, member).Trim();");
        if (flags)
        {
            source.Append(name).AppendLine(" result = default;");
            source.AppendLine("while (true) {");
            source.AppendLine("int comma = text.IndexOf(',');");
            source.AppendLine("var token = (comma < 0 ? text : text.Slice(0, comma)).Trim();");
            source.Append("if (!TryName_").Append(suffix).AppendLine("(token, out var part)) throw row.CreateConversionException(ordinal, member);");
            source.AppendLine("result |= part;");
            source.AppendLine("if (comma < 0) return result;");
            source.AppendLine("text = text.Slice(comma + 1);");
            source.AppendLine("}");
        }
        else
        {
            source.Append("if (TryName_").Append(suffix).AppendLine("(text, out var result)) return result;");
            source.AppendLine("throw row.CreateConversionException(ordinal, member);");
        }
        source.AppendLine("}");
        source.Append("private static bool TryName_").Append(suffix)
            .Append("(global::System.ReadOnlySpan<char> token, out ").Append(name).AppendLine(" value) {");
        source.AppendLine("switch (token.Length) {");
        foreach (var group in type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
                     .GroupBy(f => f.Name.Length).OrderBy(g => g.Key))
        {
            source.Append("case ").Append(group.Key).AppendLine(":");
            foreach (var field in group)
                source.Append("if (token.SequenceEqual(").Append(Literal(field.Name)).Append(")) { value = ")
                    .Append("unchecked((").Append(name).Append(")(")
                    .Append(Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture))
                    .AppendLine(")); return true; }");
            source.AppendLine("break;");
        }
        source.AppendLine("}");
        source.AppendLine("value = default; return false;");
        source.AppendLine("}");
    }

    private static string EnumConstant(string type, IFieldSymbol field) =>
        "unchecked((" + type + ")(" + Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture) + "))";

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
}
