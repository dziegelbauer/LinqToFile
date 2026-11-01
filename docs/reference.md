# Mapping and API reference

## Model requirements

A mapped model must be a public, partial, non-abstract class.
The class and its containing types must be non-generic.
Containing types must also be public and partial.
C# record declarations are not available yet.

The model must have a public parameterless constructor.
Properties can have a public setter or a public init accessor.
The generator assigns required properties in an object initializer.
An ignored required property or a required field causes a compiler error.

The generator maps inherited properties.
An override replaces the base property.
The nearest column annotation supplies the header name.
A derived column annotation can replace a base column annotation.
Conflicts between column and ignore annotations cause compiler errors.
An inherited generic base can supply properties with concrete types.
A hidden mapped base property causes a compiler error.
This rule also applies when a field or method hides that property.

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
| `WithCulture(culture)` | Returns a new source with a read-only copy of the conversion culture |
| `WithHeaderComparison(comparer)` | Returns a new source with the selected header comparison |
| `WithDelimiter(character)` | Sets the field delimiter |
| `WithQuote(character)` | Sets the quote character |
| `WithBomDetection(enabled)` | Controls BOM-based encoding selection |
| `WithFieldTrimming(enabled)` | Controls whitespace removal from field values |
| `WithHeaderTrimming(enabled)` | Controls whitespace removal from header names |
| `WithBlankLineSkipping(enabled)` | Skips or rejects entirely blank physical lines |
| `WithBufferSize(size)` | Sets the decoded-character input buffer size |
| `WithMaxFieldLength(length)` | Limits unescaped field length before trimming |
| `WithMaxRecordLength(length)` | Limits decoded source record length |
| `AsEnumerable()` | Returns the source as `IEnumerable<T>` |

The supported comparers are `StringComparer.Ordinal` and `StringComparer.OrdinalIgnoreCase`.
The default is `StringComparer.Ordinal`.
Exact column conflicts cause compiler errors.
Comparison-dependent mapping conflicts throw when `WithHeaderComparison` runs.
Duplicate file headers still throw during enumeration.
Options do not change the original source.
Options apply before standard LINQ operators.
Boolean options default to true when the caller omits the argument.
Length limits accept null to remove a limit.
All options return a new source.
The [CSV guide](csv-and-errors.md) gives the defaults and counting rules.

The generator adds `ITabularRecord<T>` to each marked class.
A generic helper must have the constraint `where T : class, ITabularRecord<T>`.
The generator supplies the mapping methods without application code.

## Current conversions

| Type | Input |
|---|---|
| `string` | The unescaped field text, including whitespace |
| Fixed-width integers | An integer with an optional sign and surrounding whitespace |
| `Half`, `float`, `double` | A finite number, with optional exponent |
| `decimal` | A number with an optional sign, decimal point, and surrounding whitespace |
| `bool` | `true` or `false`, without case sensitivity |
| `Guid` | A GUID accepted by the BCL span parser |
| `char` | Exactly one UTF-16 code unit |
| Enums | Declared case-sensitive names, with name combinations for flags |
| Temporal types | Culture-based dates, times, and durations under the offset rules |

Numeric and temporal conversions default to `InvariantCulture`.
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
The [conversion guide](conversions.md) lists all types and their rules.
Custom converters and exact-format annotations remain future goals.
