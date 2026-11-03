# Boolean and null tokens

Use `[BooleanTokens]` to read and write a boolean pair such as `yes/no` or `1/0`.
Use `[NullToken]` when a text value such as `NULL` represents null.
The same annotations apply to CSV fields, XLSX text cells, and CSV output.
Properties without them keep the [built-in conversions](conversions.md).

## Declare a model

This model accepts `yes`, `no`, or `NULL` for availability.
It keeps an empty note distinct from a null note.
Its nullable date also uses an [exact temporal format](temporal-formats.md).

```csharp
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class InventoryItem
{
    public int Id { get; init; }

    [BooleanTokens("yes", "no"), NullToken("NULL")]
    public bool? Available { get; init; }

    [NullToken("NULL")]
    public string? Note { get; init; }

    [NullToken("NULL"), TemporalFormat("yyyy-MM-dd")]
    public DateOnly? ExpiresOn { get; init; }
}
```

The example uses the implicit `System` imports and nullable reference types from a standard .NET project.
Write and query it through the usual file methods:

```csharp
var file = TabularFile.Csv<InventoryItem>(path)
    .WithDelimiter(';')
    .WithQuote('\'')
    .WithFieldTrimming();
await file.CreateAsync(new[]
{
    new InventoryItem { Id = 1, Available = true, ExpiresOn = new DateOnly(2026, 12, 31) },
    new InventoryItem { Id = 2, Available = false, Note = "" }
});
var rows = await file.ToListAsync();
```

Here, `path` is a new CSV path.
The first note writes `NULL`. The second note writes an empty field.
The [runnable sample](../samples/NativeAot/TokenSample.cs) also checks async batches, insertion, CSV escaping, and native XLSX values.
Its model belongs to a separate assembly and runs under Native AOT.

## Boolean tokens

`[BooleanTokens("yes", "no")]` selects one true token and one false token.
It supports `bool` and `bool?` properties.
It replaces the built-in `true/false` text tokens for that property.
Writing uses the exact spelling supplied in the annotation.

Matching defaults to `StringComparison.OrdinalIgnoreCase`.
Use `[BooleanTokens("Yes", "No", Comparison = StringComparison.Ordinal)]` for case-sensitive matching.
Only `Ordinal` and `OrdinalIgnoreCase` are supported.
`WithCulture` does not change token comparison.

True and false tokens must differ under the selected comparison.
Both must be nonempty strings with no leading or trailing .NET whitespace.
Internal whitespace, quotes, separators, and line breaks are literal token content.
CSV escaping still applies when writing them.

## Null tokens and empty fields

`[NullToken("NULL")]` selects one token for reading and writing null.
It supports built-in nullable value types, nullable enums, and explicitly nullable `string?` properties.
Non-nullable properties produce a compiler error.

Null matching defaults to `StringComparison.Ordinal`, which is case-sensitive.
Thus, `NULL` maps to null, while `null` remains ordinary string data.
Use `[NullToken("NULL", Comparison = StringComparison.OrdinalIgnoreCase)]` to ignore case.
Only these two comparisons are supported. They are independent of the boolean comparison.
Writing null always uses the annotation's exact token spelling.

The null token must also be nonempty and have no leading or trailing whitespace.
It adds an explicit null representation without changing empty-field rules:

| Property | Empty field | Matching null token |
|---|---|---|
| `string?` | Empty string | Null |
| Nullable value, such as `int?` | Null | Null |
| `bool?` with both annotations | Null | Null |

Quoted and unquoted empties have the same meaning.
When field trimming is enabled, it occurs before token matching.
The token reader adds no extra trimming.
For example, ` yes ` matches `yes` only when `WithFieldTrimming()` is enabled.

## Combinations and reserved values

`[NullToken]` can combine with `[BooleanTokens]` on `bool?`.
It can also combine with `[TemporalFormat]` on nullable temporal properties.
The reader checks the null token before ordinary conversion.
The writer handles null before boolean, temporal, enum, or other value formatting.

A null token reserves that decoded text for null.
Writing a non-null value that matches it throws `TabularWriteException`.
The check uses the null comparison and configured field trimming.
For example, `[NullToken("0")] int?` reads text `0` as null and rejects writing non-null zero.
`[NullToken("NULL")] string?` rejects a non-null literal `NULL`.
Quoting cannot distinguish it from null, because matching uses decoded field text.
Choose a different null token when those literal values must be written.

This check also applies to formatted dates and enum names.
Null comparison takes priority if a null token overlaps another accepted input spelling.
Boolean true/false tokens must differ from each other; a null token may reserve a value spelling.
Non-null output is checked after formatting, before CSV escaping.

## XLSX and mapping rules

Tokens apply to text cells, including shared strings and cached text formula results.
Native boolean cells keep their typed true/false values regardless of `[BooleanTokens]`.
Native numeric, boolean, and ISO date cells do not become null because their storage text matches a token.
Existing [native cell conversion rules](xlsx.md#rows-and-conversions) still apply.
Missing and empty cells keep their existing empty-field meaning.

Overrides inherit the nearest annotation of each kind.
An override can replace a boolean pair or a null token independently.
`[Column]` can rename the property.
An effective `[Ignore]` or `[ColumnConverter]` cannot combine with either token annotation.
These restrictions also apply to inherited combinations.
Custom converters already control their own parsing, formatting, and null rules.

The usual public setter/init and write getter requirements still apply.
Invalid types, token values, comparisons, or boolean pairs produce compiler diagnostic `LTF007`.
Conflicting annotations produce `LTF004`.
Read failures use `TabularConversionException`; write failures use `TabularWriteException`.
These errors keep location and property data without raw values or inner conversion exceptions.
