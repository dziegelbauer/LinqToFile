# Value conversions

These rules describe CSV text conversion.
XLSX text cells reuse these conversions; native XLSX cells have additional type rules.
See the [XLSX guide](xlsx.md) before mapping native cells.

## Supported types

The reader maps these types and their nullable value forms:

| Group | Types |
|---|---|
| Text | `string`, `char` |
| Boolean | `bool` |
| Integers | `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `Int128`, `UInt128` |
| Floating point | `Half`, `float`, `double` |
| Decimal | `decimal` |
| Identifier | `Guid` |
| Enumeration | Enum types, including flags |
| Date and time | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` |

Platform-sized `nint` and `nuint` properties, custom converters, and exact-format annotations are not supported.
Unsupported property types cause compiler diagnostic `LTF003`.

## Culture

The default conversion culture is `CultureInfo.InvariantCulture`.
`WithCulture(culture)` selects another culture.
The source keeps a read-only copy of that culture.
Later changes to the caller's culture do not change the source.
The process's current culture does not change the default.

Culture controls numeric and temporal parsing.
It does not change enum name comparison.
It does not add boolean tokens such as `yes` or `1`.

The [executable sample](../samples/NativeAot/Program.cs) uses this query:

```csharp
var scalar = TabularFile.Csv<ScalarRecord>(path)
    .WithCulture(CultureInfo.InvariantCulture)
    .WithBufferSize(1)
    .Single();
```

The sample also reads `12,50` with the `fr-FR` culture.
Its model belongs to a separate assembly.

## Empty fields

An empty field becomes an empty string for a string property.
This rule also applies to nullable string properties.
An empty field becomes null for a nullable value property.
An empty field causes a conversion error for a non-nullable value property.

Quoted and unquoted empty fields have the same meaning.
Enabled field trimming occurs before the empty-value check.
Thus, a whitespace-only field can become null for a nullable value property.

## Numbers

Integer parsing accepts a leading sign and surrounding whitespace.
Floating-point parsing also accepts decimal points and exponents.
Decimal parsing accepts a decimal point but not an exponent.

The selected culture supplies the decimal separator and sign symbols.
The parser does not accept thousands separators or currency symbols.
The integer parser does not accept hexadecimal prefixes.
Values outside the target type's range cause conversion errors.

Floating-point values must be finite.
NaN, positive infinity, negative infinity, and overflow to infinity cause conversion errors.
Numeric parsing follows BCL precision and rounding behavior.

Numeric conversion reads a span over the record buffer.
It does not make a temporary string for each field.

## Strings, characters, booleans, and GUIDs

Strings keep the unescaped field text, unless field trimming is enabled.
Each returned string owns its data.

A character field must contain exactly one UTF-16 code unit.
A supplementary character uses two code units and does not fit a C# `char`.

Boolean fields accept `true` and `false` without case sensitivity.
Other tokens cause conversion errors.
The BCL boolean parser can ignore surrounding whitespace.

GUID fields use the BCL span parser.
Its accepted GUID formats remain available.

## Enums

Enum fields accept declared names with ordinal, case-sensitive comparison.
Numeric tokens and undeclared names cause conversion errors.
Aliases are permitted when each token is a declared name.
An `[Obsolete]` member remains a declared name for parsing.

Flags fields also accept comma-separated names.
Whitespace around each name does not change the result.
An empty token causes a conversion error.
A combination does not need a separate declared name for its combined numeric value.
Negative and high-bit flags work through their declared names.

A flags value with commas must obey the CSV quote rules.
The generator supplies name comparisons and flags operations directly.
It does not use runtime enum discovery.

## Date and time

Temporal parsing uses the selected culture and the BCL parsers.
The reader first recognizes the invariant round-trip formats used by [CSV creation](csv-writing.md).
This rule preserves dates and full tick precision under non-Gregorian cultures.
Omitted date components follow BCL defaults.

`DateTime` fields reject inputs with an explicit time-zone offset.
Offset-free values keep `DateTimeKind.Unspecified` and their input clock time.

`DateTimeOffset` fields must contain an explicit offset or a recognized UTC marker.
The result keeps the input offset and clock time.
The reader does not assume the machine's local offset.
It does not normalize accepted values to local time.

`DateOnly`, `TimeOnly`, and `TimeSpan` fields use their BCL parsing rules.
Invalid dates, times, durations, and offsets cause conversion errors.

## Errors

Conversion errors use `TabularConversionException`.
The exception identifies the record, line, column, header, and target property.
It does not include the raw field value or an inner parser exception.
The [CSV guide](csv-and-errors.md) gives the shared diagnostic properties.
