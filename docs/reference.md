# Mapping and API reference

## Model requirements

A mapped model must be a public, partial, non-abstract class.
The class and its containing types must be non-generic.
Containing types must also be public and partial.
Phase 1 does not support C# record declarations.

The model must have a public parameterless constructor.
Properties can have a public setter or a public init accessor.
The generator assigns required properties in an object initializer.
An ignored required property or a required field causes a compiler error.

The generator maps inherited properties.
An override replaces the base property.
The nearest column annotation supplies the header name.
A derived column annotation can replace a base column annotation.
Conflicts between column and ignore annotations cause compiler errors.

## Attributes

| Attribute | Effect |
|---|---|
| `[TabularRecord]` | Makes a generated mapper for the partial class |
| `[Column("header")]` | Uses the literal header name for a property |
| `[Ignore]` | Excludes a property |

A property without a column annotation uses its property name.
An empty or whitespace-only annotation name causes a compiler error.
The generator preserves whitespace in a meaningful annotation name.

The generator ignores ordinary read-only properties and indexers.
A column annotation on such a member causes a compiler error.
Public fields are not mapped.

## Source methods

| Method | Effect |
|---|---|
| `TabularFile.Csv<T>(path)` | Makes a deferred file source |
| `WithEncoding(encoding)` | Returns a new source with a copy of the encoding |
| `WithHeaderComparison(comparer)` | Returns a new source with the selected header comparison |
| `AsEnumerable()` | Returns the source as `IEnumerable<T>` |

The supported comparers are `StringComparer.Ordinal` and `StringComparer.OrdinalIgnoreCase`.
The default is `StringComparer.Ordinal`.
Options do not change the original source.
Options apply before standard LINQ operators.

The generator adds `ITabularRecord<T>` to each marked class.
A generic helper must have the constraint `where T : class, ITabularRecord<T>`.
The generator supplies the mapping methods without application code.

## Current conversions

| Type | Input |
|---|---|
| `string` | The unescaped field text, including whitespace |
| `int` | An integer with an optional sign and surrounding whitespace |
| `decimal` | A number with an optional sign, decimal point, and surrounding whitespace |
| `bool` | `true` or `false`, without case sensitivity |
| `Guid` | A GUID accepted by the BCL span parser |

Numeric conversions use `InvariantCulture`.
The numeric parser does not accept thousands separators or currency symbols.
An empty field becomes an empty string.
An empty field becomes null for a nullable value property.
An empty field for a non-nullable value property causes a conversion error.
Quoted and unquoted empty fields have the same meaning.

## Compiler diagnostics

| Code | Meaning |
|---|---|
| `LTF001` | The model shape is not supported |
| `LTF002` | A member or annotation is not supported |
| `LTF003` | The property type is not supported in this phase |
| `LTF004` | The mapping contains a conflict |

An unmarked class cannot satisfy the generated interface constraint.
The C# compiler reports that use as a constraint error.
More conversion types and configurable culture are planned for later phases.
