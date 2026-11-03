# Read XLSX workbooks

For exports, use [XLSX writing](xlsx-writing.md).

## Supported workbooks

The XLSX reader supports sync/async queries and storage controls.
It accepts paths, supplied streams, sync factories, and async factories.
It reads Transitional and Strict XLSX workbooks under the rules in this guide.
It does not require Excel or an additional runtime package.

Supported cells contain shared/inline text, rich text, numbers, booleans, native ISO dates, or empty values.
Formula cells use saved cached results. The reader never calculates formulas.
Saved results can be stale. Missing cached results throw.
Text cells use the existing supported text conversions and culture option.
Rich-text runs become plain text. Visual formatting and phonetic annotations are ignored.
Spreadsheet text escapes are decoded once, independently for each text run.

Non-seekable input is copied incrementally to a temporary file before archive reading.
Headers are required. The reader uses standard LINQ and generated model mapping.

## Model and query

The sample declares this model in a separate assembly:

```csharp
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class WorkbookEntry
{
    [Column("entry_id")]
    public int Id { get; init; }
    public required string Name { get; init; }
    public bool Active { get; set; }
    public decimal? Amount { get; init; }
}
```

For a seekable workbook stream, use this query:

```csharp
var records = TabularFile.Xlsx<WorkbookEntry>(stream)
    .WithWorksheet("Data")
    .Where(record => record.Active)
    .ToArray();
```

Pass a file path instead of the stream to open a workbook by path.
The generated mapper uses the same annotations as CSV.

## Async sources and queries

Use `Xlsx<T>(Func<Stream>)` for a synchronous stream factory.
Use `XlsxAsync<T>(Func<CancellationToken, ValueTask<Stream>>)` for an async-only factory.
Each factory must return a fresh readable stream.

The [async sample](../samples/NativeAot/WorkbookAsyncSample.cs) checks this path query:

```csharp
var entries = TabularFile.Xlsx<WorkbookEntry>(path);
var activeEntries = await entries.AsAsyncEnumerable()
    .Where(entry => entry.Active)
    .ToArrayAsync();
var byId = await entries.ToDictionaryAsync(entry => entry.Id);
```

The configured source supports direct `await foreach`.
Use `AsAsyncEnumerable()` for BCL async LINQ operators.
`ToListAsync`, `ToArrayAsync`, and both `ToDictionaryAsync` overloads accept cancellation.
The [source guide](sources-and-async.md) gives the full signatures and ownership rules.

Async queries use async reads for workbook parts and temporary storage.
ZIP directory loading, seeks, and file creation remain synchronous.
A supplied stream must support the operations its selected query mode requires.
An async-only factory rejects sync enumeration without calling the factory.

Cancellation reaches archive reads, copying, shared-string processing, preflight, and buffered XML parsing.
Owned sources and temporary files close on cancellation, failure, completion, or early exit.
One synchronous metadata operation can finish before cancellation is observed.

## Options and ownership

| Option | Behavior |
|---|---|
| `WithWorksheet(name)` | Select a worksheet by name with ordinal, case-insensitive matching |
| `WithHeaderRow(number)` | Select a physical worksheet row, starting at 1 |
| `WithCulture(culture)` | Select the conversion culture for text cells |
| `WithHeaderComparison(comparer)` | Use ordinal or ordinal, case-insensitive header matching |
| `WithHeaderTrimming()` | Trim headers before matching |
| `WithFieldTrimming()` | Trim field values before conversion |
| `WithSharedStringMemoryBudget(bytes)` | Set the retained shared-string budget; default 8 MiB |
| `WithTemporaryFiles(enabled)` | Permit or forbid disk spill and workbook copying; default true |
| `WithTemporaryDirectory(path)` | Select the directory for generated temporary files |
| `WithMaxWorkbookBytes(bytes)` | Limit archive bytes from the stream origin through its end |
| `WithMaxDecompressedBytes(bytes)` | Limit all decompressed bytes read, including repeated reads |
| `WithMaxCellTextLength(length)` | Limit resolved UTF-16 cell text before trimming |
| `WithMaxTemporaryBytes(bytes)` | Limit aggregate live temporary-file lengths |

Options are immutable. Invalid arguments throw immediately.
The default is the first worksheet in workbook order, including hidden worksheets.
The default header is the first row with content.
Empty cells and empty text do not count as content. Zero and false do.

Paths and factories open an independent stream for each enumeration.
Supplied streams permit one enumeration across all derived configurations.
The stream starts at its current position and remains open by default.
Set `leaveOpen: false` to transfer disposal responsibility to the library.
The reader does not restore the original stream position.

## Rows and conversions

The header range starts at column A and ends at its last non-empty value.
Missing mapped headers, duplicate headers, and gaps in the header range throw.
Additional named columns can remain unmapped.
Sparse data cells retain their physical column positions.
Missing cells become empty values. Existing nullable and string rules apply.
Values beyond the header range throw; trailing empty or formatting-only cells are ignored.
Blank rows are skipped. Whitespace counts as content before trimming.

Native numeric cells use invariant parsing, independent of display styles and `WithCulture`.
Integer targets accept decimal/exponent notation only when the value is integral and in range.
Native boolean cells map to booleans or strings, not numbers.
Numeric cells cannot become booleans, enums, characters, or GUIDs.
String targets receive numeric storage text or canonical true/false for boolean cells.
No cell display formatting is rendered.

These are the built-in conversion rules.
A [custom converter](custom-converters.md) can accept other mappings for an annotated property.
It receives the stored value, cell kind, configured culture, and workbook date system.
It also receives empty fields after any configured trimming.

[Temporal format annotations](temporal-formats.md) select exact formats for text cells.
Native numeric and ISO date cells retain the date rules below.

[Boolean and null tokens](column-tokens.md) also apply to text cells.
Native cell values retain their existing typed meaning.

## Dates and times

Numeric temporal conversion is selected by the target property type, not the cell display style.

| Target | Native numeric cell | Native ISO date cell |
|---|---|---|
| `DateTime` | Workbook serial date; unspecified kind | Offset-free date or full date-time |
| `DateOnly` | Whole-day serial only | Date-only form only |
| `DateTimeOffset` | Rejected; no offset is stored | Full date-time with an explicit offset |
| `TimeOnly` | Fraction of a day, from 0 inclusive to 1 exclusive | Rejected |
| `TimeSpan` | Signed elapsed days, including fractions and multiple days | Rejected |
| `string` | Stored numeric text | Stored ISO text |

The workbook selects the 1900 or 1904 date system.
This reader maps serial zero to 1899-12-31 or 1904-01-01, respectively.
Negative calendar serials and the fictitious 1900-system day 60 throw.
Durations do not use calendar or leap-day rules.
`DateOnly` rejects fractional days. `TimeOnly` never wraps whole days.

Native numeric temporal values use exact decimal arithmetic before one rounding step.
The rounding unit is a 100-nanosecond tick, with midpoint-to-even rounding.
Out-of-range values, rounding onto the fictitious day, and `TimeOnly` rounding to 24:00 throw.
There is no local-time-zone inference or conversion.

Native ISO cells accept YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss.
The full date-time can include fractional seconds and Z or a signed HH:mm offset.
Fractional seconds use the same tick-rounding rule for typed temporal targets.
Leap seconds, 24:00, invalid dates, and invalid offsets throw.
String properties preserve stored ISO text, including its precision.

## Shared strings and limits

The shared-string table is processed before records are returned.
When its retained text and index fit the budget, it stays in memory.
If it would exceed the budget, both content and a fixed-width index move to temporary files.
The reader does not retain a growing offset array in memory after spill.
In-memory strings can be reused as immutable returned values when trimming does not change them.
Spilled strings are copied into the active row buffer and returned as owned values.

The memory budget is an accounting limit for retained shared-string storage, not a process-memory cap.
Active row/text buffers, ZIP/workbook metadata, merge metadata, and caller-retained results are separate.
The reader creates temporary files only for spill or non-seekable input.
It deletes owned temporary files on disposal, failure, or cancellation.
It does not create the configured directory. That directory must exist when temporary storage is needed.
If temporary files are disabled, exceeding the memory budget throws instead of using unbounded RAM.
A zero memory budget sends any shared-string entries to disk, including empty strings.

Maximum-size limits default to null, which means unlimited.
Zero is valid. Negative arguments throw immediately.
The decompressed-byte limit includes metadata, the shared-string table, preflight, and reopened worksheet reads.
The cell limit counts resolved UTF-16 code units after XML/text-escape decoding and rich-text flattening, before trimming.
It applies to headers, unmapped columns, and shared-string items during preprocessing, including unused items.
Native boolean text counts its canonical true/false representation.
Temporary bytes count live logical file lengths, not cumulative writes or filesystem block allocation.
The disk index uses 12 bytes per string, in addition to two bytes per UTF-16 code unit.
Non-seekable input adds its complete compressed workbook length to the same temporary-byte budget.
The workbook-byte limit is checked while copying, before an oversized chunk is written.
With temporary files disabled, non-seekable input fails before reading source bytes.

The extended [sample model](../samples/Models/WorkbookCells.cs) includes all temporal target types.
Its query forces disk storage to exercise spill and limits:

```csharp
var row = TabularFile.Xlsx<WorkbookCells>(stream)
    .WithSharedStringMemoryBudget(0)
    .WithMaxTemporaryBytes(64 * 1024)
    .WithMaxDecompressedBytes(1024 * 1024)
    .WithMaxCellTextLength(128)
    .Single();
```

## Validation and errors

Before yielding records, the reader scans worksheet metadata for merged ranges.
It then reopens the worksheet XML for streaming.
This requires reading the worksheet part before the first record is returned.
It does not materialize the worksheet or convert future data rows.
Merges overlapping the header columns from the header row onward throw, including in trailing blank rows.
Merged titles entirely above an explicitly selected header are allowed.

Malformed package structure, invalid native cells, missing formula caches, and exceeded resource limits throw `XlsxFormatException`.
Schema and conversion errors use the existing shared exception types.
Errors expose `WorksheetName`, `WorksheetRowNumber`, `CellReference`, and `PartName` when available.
`ColumnIndex` is the physical worksheet column, starting at 1.
`RecordNumber` counts logical records, with the header as record 1; 0 means unknown.
CSV line-number fields are 0 for XLSX. They do not represent worksheet rows.
Messages omit raw values and formula text.
I/O failures keep their standard exception types.

The reader prohibits DTDs and external XML resolution.
It rejects external relationships needed to read the selected workbook and worksheet.
It does not fetch external data, calculate formulas, or execute macros.
Legacy, binary, macro-enabled, and encrypted workbook formats are outside this scope.

## Add records to an existing workbook

Path sources return `XlsxFile<T>`, which also supports `Append`, `AppendRange`, `Insert`, `InsertRange`, and their async variants.
See [XLSX append and insertion](xlsx-mutation.md) for supported workbooks, placement, validation, and staged replacement.
Stream and factory sources remain read sources.
