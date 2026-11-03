# Export XLSX workbooks

Use `TabularFile.XlsxWriter<T>` to export typed records to a new workbook.
Each operation writes one worksheet, its header, and optional data rows.
You can export LINQ results without collecting the full sequence first.
The writer supports paths, writable streams, and stream factories.

## Export to a path

This example uses the `Person` model from the [introduction](../README.md).

```csharp
var writer = TabularFile.XlsxWriter<Person>(outputPath)
    .WithWorksheet("People");
long written = await writer.WriteAsync(new[]
{
    new Person { Id = 1, Name = "Ada", Age = 36 },
    new Person { Id = 2, Name = "Grace", Age = 17 }
});
var people = await TabularFile.Xlsx<Person>(outputPath)
    .WithWorksheet("People")
    .ToArrayAsync();
```

The default worksheet name is `Sheet1`.
Headers start at A1; data starts in row 2.
Columns use ordinal header-name order, as with new CSV documents.
The usual generated mapping and accessible-getter requirements apply.

Path output throws if the destination exists.
Select `.WithExistingFileBehavior(ExistingFileBehavior.Overwrite)` to replace it intentionally.
Overwrite also creates a missing destination.
This option is also available for [CSV creation](csv-writing.md).

All XLSX path exports prepare a temporary file beside the destination.
Only a completed workbook is published.
An export error or cancellation before publication preserves the destination and removes owned temporary output.
Cancellation stops immediately before publication; once publication begins, success reports success.
Default publication does not overwrite a file created by another writer.
Explicit overwrite permits replacement at publication time.
There is no crash-durability or cross-process coordination guarantee.
On Unix, overwrite restricts temporary permissions to the original access bits and the process mask.
Ownership, ACLs, and other metadata are not generally preserved.

## Export to a stream

```csharp
using var stream = new MemoryStream();
await TabularFile.XlsxWriter<Person>(stream)
    .WithWorksheet("People")
    .WriteAsync(people);
stream.Position = 0;
var copied = await TabularFile.Xlsx<Person>(stream).ToArrayAsync();
```

Supplied streams stay open by default. Set `leaveOpen: false` to transfer disposal to the export attempt.
One supplied destination allows one attempt across all derived configurations.
The attempt is claimed after argument validation and before cancellation is checked.
Failure or cancellation consumes it, including a pre-cancelled attempt.
Configuration alone does not consume it or transfer disposal of an unused stream.

Writing begins at the current position without reading, seeking, rewinding, or truncating.
Non-seekable streams are supported.
Prepare reusable destinations yourself: leftover trailing bytes can invalidate a workbook.
Separate writer instances do not coordinate access to the same stream.

`XlsxWriter<T>(Func<Stream>)` opens a fresh, library-owned stream per export.
It supports both sync and async methods; the factory itself runs synchronously.
`XlsxWriterAsync<T>(Func<CancellationToken, ValueTask<Stream>>)` supports async exports only.
A synchronous operation throws without invoking an async-only factory.
Factories can export repeatedly or concurrently when they supply independent streams and support concurrent calls.
Owned streams are disposed on success, error, or cancellation. Async exports await asynchronous disposal.

Calling `WithExistingFileBehavior` on a stream or factory writer throws immediately.
It is a path-only option.

## Operations and options

| Operation | Output |
|---|---|
| `Write()` / `WriteAsync(token)` | Header only; returns zero |
| `Write(record)` / `WriteAsync(record, token)` | Header and one data row |
| `Write(records)` | Header and incremental `IEnumerable<T>` rows |
| `WriteAsync(records, token)` | Header and incremental `IEnumerable<T>` or `IAsyncEnumerable<T>` rows |

Successful operations return a `long` or `ValueTask<long>` data-row count, excluding the header.
Input is enumerated once. Empty input still produces a workbook with headers.

Options return new configurations:

- `WithWorksheet(name)` selects the output name, preserving meaningful whitespace. Invalid names throw immediately.
- `WithDateSystem(TabularDateSystem.Excel1900)` is the default. `Excel1904` is also supported; `None` is rejected.
- `WithCulture` takes a read-only snapshot for text formatters and converters. Native numbers and dates are culture-independent.
- `WithHeaderComparison` accepts ordinal or ordinal-ignore-case comparison for generated-header validation.
- `WithHeaderTrimming` validates compatibility with trimmed header reads.
- `WithFieldTrimming` controls text null-token collision checks; it does not trim output.
- `WithMaxCellTextLength` can impose a smaller text limit. Null leaves the workbook format's limit in effect; negative values throw.

Worksheet names follow [Excel's naming rules](https://support.microsoft.com/en-gb/excel/rename-a-worksheet), including the 31-character limit and reserved name `History`.

## Cell types and precision

| Property or annotation | Output |
|---|---|
| Built-in numeric types | Native numeric cell, invariant storage |
| `bool` | Native boolean cell |
| `string`, `char`, `Guid`, enums | Literal text |
| `DateOnly`, `DateTime` | Native serial date with a date/date-time display format |
| `TimeOnly` | Native fraction of a day with a time display format |
| `DateTimeOffset` | Invariant ISO 8601 text preserving clock time and offset |
| `TimeSpan` | Invariant `c` text preserving sign, days, and ticks |
| `TemporalFormat`, `BooleanTokens` | Text using the selected format/culture or literal tokens |
| Custom converter | Literal text from `TryFormat`, with `Text`/`None` context |

Numeric output throws when it requires more than 15 significant decimal digits or exceeds the supported Excel numeric range.
Trailing zeros do not add significant precision. Floating-point values use their round-trip text when checking precision.
Non-finite values and unsupported tiny values throw; values are never silently rounded to fit.
Use an explicit text converter when preserving a large or precise number is more useful than a numeric cell.

Native dates start at 1900-01-01, or 1904-01-01 for that system, and extend through year 9999.
`DateTime` requires `Kind.Unspecified`.
Native temporal output must round-trip to the original .NET ticks through the reader under the numeric precision limit.
Values that fail this check throw, including many ordinary timestamps with time components.
Use `TemporalFormat("O")` or a converter for full-precision text output.
The display formats show dates or times with milliseconds; display formatting does not change the stored value.

## Nulls, text, and limits

Null becomes a blank cell unless `NullToken` selects a literal text token.
Non-null text matching its null token throws, using the configured comparison and field trimming.
Native cells do not collide with text null tokens: `[NullToken("0")] int?` can write numeric zero.
Custom converters retain control of null output.
Use an explicit null token when you need to distinguish null strings from empty strings.

A record that produces only blank cells or empty text throws, because the XLSX reader skips entirely blank rows.
Numeric zero, boolean false, whitespace, and explicit null tokens count as content.

Text is stored directly in each cell without a workbook-wide shared-string dictionary.
Whitespace and literal spreadsheet-escape sequences are preserved.
Text beginning with `=` remains text; formulas are not inferred.
XML characters and UTF-16 values use the required XML/spreadsheet escaping without changing the resolved value.

Output is limited to 1,048,575 data rows plus the header, 16,384 columns, and 32,767 UTF-16 code units per cell.
Cell text can contain at most 253 line feeds.
Limits include headers and formatted converter/token output, before escaping.
Exceeding a limit throws; the writer does not truncate or split data into extra worksheets.

## Completion and errors

Successful operations finish the ZIP archive, flush output, and complete owned disposal before returning.
Async export uses asynchronous destination writes and flushing, including ZIP entry-finalization output.
Input callbacks, synchronous factories, and custom streams can perform their own synchronous work.
No `Task.Run` or blocking wait is used to wrap output.

Cancellation remains active throughout async stream export and archive completion.
Failure or cancellation can leave a partial, unreadable workbook in a stream; there is no rollback or temporary staging for streams.
Owned cleanup still runs. Cleanup errors do not replace an existing export error, except `OutOfMemoryException`.
An owned-disposal failure after otherwise successful output is reported as a failure.

Serialization errors use private `TabularWriteException` diagnostics with input-row, worksheet-row, header/member, and cell coordinates when available.
Header/model errors use existing schema/write exceptions.
I/O, factory, input-enumeration, and cancellation exceptions retain their standard types.
Raw values and inner conversion exceptions are omitted.

The writer exports a plain worksheet in Transitional XLSX format.
It does not edit existing workbooks, create formulas/tables/filters, or provide general layout styling.
See the [roadmap](roadmap.md) for future work and the [runnable sample](../samples/NativeAot/XlsxWritingSample.cs) for more examples.
