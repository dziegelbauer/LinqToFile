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
public sealed class RecordGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidModel = Rule("LTF001", "Unsupported record model");
    private static readonly DiagnosticDescriptor InvalidMember = Rule("LTF002", "Unsupported record member");
    private static readonly DiagnosticDescriptor InvalidType = Rule("LTF003", "Unsupported property type");
    private static readonly DiagnosticDescriptor Conflict = Rule("LTF004", "Conflicting column mapping");

    private static DiagnosticDescriptor Rule(string id, string title) =>
        new DiagnosticDescriptor(id, title, "{0}", "LinqToFile", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
            "LinqToFile.TabularRecordAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);
        context.RegisterSourceOutput(models, static (output, model) => Emit(output, model));
    }

    private static void Emit(SourceProductionContext context, INamedTypeSymbol model)
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
                bool ignored = false;
                for (var declaration = property; declaration != null; declaration = declaration.OverriddenProperty)
                {
                    foreach (var attribute in declaration.GetAttributes())
                    {
                        var name = attribute.AttributeClass?.ToDisplayString();
                        if (name == "LinqToFile.ColumnAttribute" && column == null) column = attribute;
                        if (name == "LinqToFile.IgnoreAttribute") ignored = true;
                    }
                }

                // The most-derived override already includes effective annotations from the chain.
                if (seen.TryGetValue(property.Name, out var derived) &&
                    derived is IPropertySymbol derivedProperty && Overrides(derivedProperty, property))
                    continue;

                if (ignored && column != null)
                {
                    Error(Conflict, property, "Column and Ignore cannot apply to the same effective property.");
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
                    if (column != null || property.IsRequired)
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
                var reader = ReadExpression(valueType, index, property.Name);
                if (reader == null)
                {
                    Error(InvalidType, property, "Use a supported scalar type or enum. Custom converters, nint, and nuint are not supported yet.");
                    continue;
                }
                if (valueType.TypeKind == TypeKind.Enum)
                    enums.Add(new KeyValuePair<int, INamedTypeSymbol>(index, (INamedTypeSymbol)valueType));
                properties.Add(property);
                columns.Add(header!);
                readers.Add(reader);
                nullable.Add(isNullable);
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
            if (nullable[i]) source.Append("row.IsEmpty(ordinals[").Append(i).Append("]) ? null : ");
            source.Append(readers[i]).AppendLine(",");
        }
        source.AppendLine("};");
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
        foreach (var owner in containers) source.AppendLine("}");
        if (!model.ContainingNamespace.IsGlobalNamespace) source.AppendLine("}");
        context.AddSource(mapper + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static bool Overrides(IPropertySymbol derived, IPropertySymbol ancestor)
    {
        for (var current = derived.OverriddenProperty; current != null; current = current.OverriddenProperty)
            if (SymbolEqualityComparer.Default.Equals(current, ancestor)) return true;
        return false;
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
        source.AppendLine("var text = row.GetField(ordinal).Trim();");
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

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
}
