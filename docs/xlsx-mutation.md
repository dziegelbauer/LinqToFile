# Append and insert XLSX rows

Use `TabularFile.Xlsx<T>(path)` to add records to an existing workbook.
Use [XLSX writing](xlsx-writing.md) to create a workbook first.
The append and insert methods use the same names and input types as [CSV writing](csv-writing.md).

## Add records

Select an existing worksheet, then insert or append records:

```csharp
var file = TabularFile.Xlsx<Person>(path).WithWorksheet("People");
await file.InsertAsync(1, new Person { Id = 2, Name = "Grace", Age = 37 });
await file.AppendRangeAsync(new[]
{
    new Person { Id = 4, Name = "Mary", Age = 30 }
});
var people = await file.ToArrayAsync();
```

`Insert` and `InsertAsync` take a zero-based data-record index.
Index zero inserts before the first data record. An index equal to the record count appends.
Blank rows and the header do not count as data records.
An index outside this range throws.

Each method returns the number of records written as `long` or `ValueTask<long>`.
Use `AppendRange` or `InsertRange` for an `IEnumerable<T>`.
Their async variants accept either `IEnumerable<T>` or `IAsyncEnumerable<T>`.
For example, append records from another file without collecting them first:

```csharp
var incoming = TabularFile.Csv<Person>(csvPath).AsAsyncEnumerable();
long appended = await file.AppendRangeAsync(incoming, cancellationToken);
```

`Append`, `AppendAsync`, `Insert`, and `InsertAsync` each accept one record.
All async methods accept an optional cancellation token.
Each operation enumerates its input once.

## Select and map the table

The path must exist. Stream and stream-factory read sources do not expose these methods.
The first worksheet in workbook order is the default, even when it is hidden.
Use `WithWorksheet` to select an existing sheet by name, with ordinal case-insensitive matching.
Use `WithHeaderRow` to select the physical, one-based header row.

Every mapped property must have a header, and every header must map to a property.
Extra columns, missing columns, and duplicate headers throw.
Header comparison and trimming follow the configured read options.
New values follow the existing header order.
Model annotations, custom converters, culture, text limits, and the workbook date system apply to new records.
See [XLSX writing](xlsx-writing.md) for native number and date precision rules.

Existing records are checked for valid workbook structure and native cell syntax.
They are not converted into your C# model.
For example, an existing text cell that cannot convert to an integer can remain unchanged during an append.

## Placement and preservation

Insertion moves the selected data row and all later physical rows down.
Blank gaps before that row keep their positions.
Append adds rows after the last physical row, including blank or formatted rows.
A worksheet dimension hint does not determine this position.
An empty range validates the workbook and index, then returns zero without changing the file.

Existing values, cell styles, shared-string indexes, worksheet names, order, and visibility are preserved.
Untouched package parts retain their uncompressed bytes. ZIP compression bytes can change.
New text uses inline strings. New date and time cells reuse or add number formats without changing existing style indexes.
New rows do not copy adjacent row styles.
Freeze panes, split panes, scroll positions, and selections keep their physical coordinates.
They do not follow moved records.

## Supported workbooks

Mutation supports plain tabular Transitional and Strict `.xlsx` workbooks with multiple worksheets.
Formulas, tables, charts, drawings, named ranges, external relationships, and other unsupported reference features cause rejection anywhere in the workbook.
Unknown unsafe package content also causes rejection.
The library does not remove unsupported features to complete a write.

Workbook protection and protection on the selected sheet cause rejection.
Protection on another sheet can remain.
Merged cells on the selected sheet must be wholly above the header.
Other sheets can retain merged cells.
The worksheet limit is 1,048,576 physical rows, including blank and formatted rows.
Legacy, macro-enabled, binary, and encrypted workbooks are not supported.

## Publication, cancellation, and limits

Each operation writes a temporary workbook beside the original, then replaces the original file.
Input, conversion, validation, cancellation, or output errors before replacement leave the original unchanged.
Replacement has no destructive fallback.
Cancellation is checked until replacement starts. A successful replacement is not reported as canceled.
There is no in-place or unchecked append mode for XLSX.

Operations on the same normalized path in this process cannot overlap.
This guard does not coordinate other processes, path aliases, or an open Excel application.

`WithTemporaryFiles(false)` rejects mutation, including empty ranges.
`WithTemporaryDirectory` controls auxiliary files; the replacement file stays beside the workbook.
`WithMaxTemporaryBytes` bounds the total live logical size of owned temporary files.
`WithMaxWorkbookBytes` bounds both the source and completed compressed output.
`WithMaxDecompressedBytes` counts every read of a source package part, including repeated passes and copying unchanged parts.
Generated XML and reads of the new-row spool do not count as source decompression.
See [XLSX workbooks](xlsx.md) for the remaining read and storage options.

Errors use the existing XLSX exception types and location properties.
Raw field values are excluded from library error messages.
