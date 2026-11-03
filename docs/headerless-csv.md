# Read headerless CSV

Use headerless reading when the first CSV record contains data.
Choose one of two mapping modes before you apply LINQ operators.
Both modes support paths, streams, stream factories, and async reading.

## Supply column names

Supply names in file order with `WithColumnNames`.
The names use the existing property-name and `[Column]` matching rules.
The method copies the names, so later changes to your array cannot change the source.

For a file with records such as `1,unused,Ada`, use:

```csharp
var namedEntries = await TabularFile.Csv<HeaderlessEntry>(path)
    .WithColumnNames("id", "unused", "name")
    .ToArrayAsync();
```

Supply at least one name.
Names cannot be null, empty, or whitespace-only.
Header trimming and comparison options apply to these names.
Duplicate names and missing mapped columns cause immediate configuration errors.
Every record must have exactly the supplied number of fields.
Unmapped columns are permitted.

## Map field positions

Add `[ColumnIndex]` to each mapped property.
Indexes start at zero.
Use `[Ignore]` for excluded properties.

```csharp
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class HeaderlessEntry
{
    [Column("id"), ColumnIndex(0)]
    public int Id { get; init; }

    [Column("name"), ColumnIndex(2)]
    public required string Name { get; init; }
}
```

Select index mapping explicitly:

```csharp
var entries = TabularFile.Csv<HeaderlessEntry>(path)
    .WithColumnIndexes()
    .ToList();
```

This model reads the first and third fields.
It skips the second field.
Gaps and unmapped trailing fields are permitted.
All fields receive CSV syntax and size-limit checks.
The first record must contain every mapped index and sets the field count for later records.
Every later record must have the same field count.

Negative or duplicate indexes cause compiler errors.
Missing indexes cause a configuration error when you call `WithColumnIndexes()`.
An overriding property uses the nearest index annotation in its override chain.
Combining `[Ignore]` with an effective `[ColumnIndex]` causes a compiler error.

## Switch mapping modes

`WithColumnNames(...)`, `WithColumnIndexes()`, and `WithHeaders()` each return a new source.
Each method replaces the previous mapping mode.
Earlier configurations remain unchanged.
`WithHeaders()` restores reading names from the first record.
Header-based reading is the default.

A property can have both `[Column]` and `[ColumnIndex]`.
Named mapping uses `[Column]`; index mapping uses `[ColumnIndex]`.
Adding an index annotation alone does not select headerless reading.

## Empty input, errors, and output

Empty headerless input returns no records.
Input with only skipped blank lines also returns no records.
Configuration validation still applies.
The first data record is logical record 1.
Physical line numbers include skipped blank lines and lines inside quoted fields.
Diagnostic column indexes remain one-based.
Index mapping has no column headers for error diagnostics.

Conversion, encoding, cancellation, and stream ownership rules remain the same.
See [CSV rules and errors](csv-and-errors.md) and [sources and async queries](sources-and-async.md).

Headerless modes apply only to CSV reading.
XLSX still requires headers.
`[ColumnIndex]` does not change XLSX reads or output column order.
CSV creation, append, and insertion retain their header-based contracts, including on a source with headerless read options.
Use `WithHeaders()` to read a CSV document created by those operations.
