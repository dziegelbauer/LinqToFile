using System;
using System.Collections.Generic;
using System.Linq;
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
            context.ReportDiagnostic(Diagnostic.Create(rule, symbol.Locations.FirstOrDefault(), message));
        }

        bool accessible = true;
        for (var owner = model; owner != null; owner = owner.ContainingType)
            accessible &= owner.DeclaredAccessibility == Accessibility.Public && owner.Arity == 0 &&
                owner.DeclaringSyntaxReferences.All(r =>
                    r.GetSyntax() is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        if (model.TypeKind != TypeKind.Class || model.IsRecord || model.IsAbstract || model.IsStatic ||
            !accessible || !model.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0))
        {
            Error(InvalidModel, model, "Phase 1 requires a public, partial, non-generic, non-abstract class with a public parameterless constructor. Containing types must be public, partial, and non-generic. Record classes are not yet supported.");
            return;
        }

        var properties = new List<IPropertySymbol>();
        var columns = new List<string>();
        var readers = new List<string>();
        var nullable = new List<bool>();
        var seen = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        var columnSet = new HashSet<string>(StringComparer.Ordinal);
        for (var type = model; type != null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsRequired))
                Error(InvalidMember, field, "Required fields are not supported. Use a property.");

            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic) continue;
                if (seen.TryGetValue(property.Name, out var derived))
                {
                    var ancestor = derived.OverriddenProperty;
                    while (ancestor != null && !SymbolEqualityComparer.Default.Equals(ancestor, property))
                        ancestor = ancestor.OverriddenProperty;
                    if (ancestor == null)
                        Error(Conflict, property, "A hidden property creates an ambiguous mapping.");
                    continue;
                }
                seen.Add(property.Name, property);

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
                if (property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public ||
                    property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    if (column != null || property.IsRequired)
                        Error(InvalidMember, property, "Mapped and required properties need a public setter or public init accessor and cannot be indexers.");
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
                string? reader = null;
                switch (valueType.SpecialType)
                {
                    case SpecialType.System_String: reader = "ReadString"; break;
                    case SpecialType.System_Int32: reader = "ReadInt32"; break;
                    case SpecialType.System_Decimal: reader = "ReadDecimal"; break;
                    case SpecialType.System_Boolean: reader = "ReadBoolean"; break;
                }
                if (valueType.ToDisplayString() == "System.Guid") reader = "ReadGuid";
                if (reader == null)
                {
                    Error(InvalidType, property, "This phase supports string, int, decimal, bool, Guid, and nullable value forms. Other conversion types are planned.");
                    continue;
                }
                properties.Add(property);
                columns.Add(header!);
                readers.Add(reader);
                nullable.Add(isNullable);
            }
        }
        if (failed) return;

        string modelName = model.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // Injective identifier encoding, independent of process-randomized string hashes.
        string id = string.Concat(modelName.Select(c => ((int)c).ToString("x4")));
        string mapper = "Mapper_" + id;
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine("namespace LinqToFile.Generated {");
        source.Append("internal sealed class ").Append(mapper)
            .Append(" : global::LinqToFile.Infrastructure.RecordMapper<").Append(modelName).AppendLine("> {");
        source.Append("internal static readonly ").Append(mapper).Append(" Instance = new ").Append(mapper).AppendLine("();");
        source.Append("private static readonly string[] names = new string[] {");
        foreach (var column in columns) source.Append(Literal(column)).Append(",");
        source.AppendLine("};");
        source.AppendLine("public override global::System.ReadOnlySpan<string> Columns => names;");
        source.Append("public override ").Append(modelName)
            .AppendLine(" Read(global::LinqToFile.Infrastructure.CsvRecord row, global::System.ReadOnlySpan<int> ordinals) =>");
        source.Append("new ").Append(modelName).AppendLine(" {");
        for (int i = 0; i < properties.Count; i++)
        {
            source.Append("@").Append(properties[i].Name).Append(" = ");
            if (nullable[i]) source.Append("row.GetField(ordinals[").Append(i).Append("]).IsEmpty ? null : ");
            source.Append("row.").Append(readers[i]).Append("(ordinals[").Append(i).Append("], ")
                .Append(Literal(properties[i].Name)).AppendLine("),");
        }
        source.AppendLine("};");
        source.AppendLine("} }");
        if (!model.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(model.ContainingNamespace.ToDisplayString()).AppendLine(" {");
        var containers = new Stack<INamedTypeSymbol>();
        for (var owner = model.ContainingType; owner != null; owner = owner.ContainingType) containers.Push(owner);
        foreach (var owner in containers)
        {
            var kind = owner.IsRecord ? (owner.TypeKind == TypeKind.Struct ? "record struct" : "record class")
                : owner.TypeKind == TypeKind.Struct ? "struct" : "class";
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

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
}
