# XLSX foundation

## Current scope

XLSX stage 1 is an experimental reader foundation.
It supports synchronous reading from paths and seekable streams.
It reads the agreed subset of Transitional and Strict workbooks.
It does not require Excel or an additional runtime package.

Supported cells contain simple inline text, numeric values, booleans, or empty values.
Shared strings, formulas, rich text, spreadsheet text escapes, and native ISO date cells are not supported yet.
The reader throws when it encounters those features in the table.
Numeric serial dates cannot yet map to temporal properties.
Text cells can use the existing supported text conversions and culture option.
Ordinary Excel files often use shared strings, so many workbooks require the next implementation stage.

Async XLSX queries, factories, non-seekable input, temporary storage, and resource-limit options are planned.
They are not available in this stage.

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

## Options and ownership

| Option | Behavior |
|---|---|
| `WithWorksheet(name)` | Select a worksheet by name with ordinal, case-insensitive matching |
| `WithHeaderRow(number)` | Select a physical worksheet row, starting at 1 |
| `WithCulture(culture)` | Select the conversion culture for text cells |
| `WithHeaderComparison(comparer)` | Use ordinal or ordinal, case-insensitive header matching |
| `WithHeaderTrimming()` | Trim headers before matching |
| `WithFieldTrimming()` | Trim field values before conversion |

Options are immutable. Invalid arguments throw immediately.
The default is the first worksheet in workbook order, including hidden worksheets.
The default header is the first row with content.
Empty cells and empty text do not count as content. Zero and false do.

Paths open an independent stream for each enumeration.
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

## Validation and errors

Before yielding records, the reader scans worksheet metadata for merged ranges.
It then reopens the worksheet XML for streaming.
This requires reading the worksheet part before the first record is returned.
It does not materialize the worksheet or convert future data rows.
Merges overlapping the header columns from the header row onward throw, including in trailing blank rows.
Merged titles entirely above an explicitly selected header are allowed.

Malformed package structure and unsupported cell features throw `XlsxFormatException`.
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

## Verify the sample

Run `dotnet run --project samples/NativeAot -c Release` from the repository root.
The sample checks both workbook variants and prints this line:

```text
XLSX: Transitional and Strict; typed records: Ada
```

The package-reference and Native AOT sample checks run the same code.
The [XLSX plan](spec/xlsx-plan.md) describes the remaining stages.
