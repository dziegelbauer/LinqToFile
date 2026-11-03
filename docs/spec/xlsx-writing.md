# XLSX writing design

## Confirmed
- The user approved a separate TabularFile.XlsxWriter<T>() API and requires consistent API design across file types. Follow the CsvWriter<T> naming and immutable configuration conventions where applicable.
- Preserve .NET 10+, cross-platform support, Native AOT, BCL-only runtime, no runtime reflection, and minimized allocations.
- Plan behavior with the user one decision at a time before implementation. A suggestion is not a confirmed contract.

## Proposed remaining scope
- Defer append, insertion, and editing existing workbooks.

## Confirmed existing-file behavior
- Add the typed option WithExistingFileBehavior(ExistingFileBehavior.Overwrite), with ExistingFileBehavior.Throw as the default. The user approved intentional overwrite for XLSX path exports and the same option for CSV Create/CreateAsync.
- Overwrite prepares complete output in a temporary file and replaces the destination only after output succeeds, so export failures preserve the original. This applies to complete-document path output; CSV append/insert behavior stays unchanged. Supplied streams retain their separate current-position/partial-output contract.
- This is an approved planned API, not implemented behavior. Update human guides only when implementation is available.
- The user approved rejecting CSV creation when both WithInPlaceWrites() and ExistingFileBehavior.Overwrite are selected. Do not silently weaken staged overwrite protection. This restriction does not change append/insert behavior.

## Confirmed worksheet scope
- Export one worksheet per workbook initially, named Sheet1 by default. WithWorksheet(string name) selects its output name. The user approved this scope; multiple worksheets remain future work.

## Confirmed destinations and lifetime
- Support paths, supplied writable streams, synchronous stream factories, and asynchronous stream factories initially. The user approved matching the CSV stream lifetime and reuse contracts.
- Supplied streams default to leaveOpen=true, with one export attempt shared across derived configurations. Factory streams are fresh, library-owned, and disposed after each export; asynchronous exports await asynchronous disposal. Factory-backed configurations permit repeated/concurrent exports with independent destinations and buffers.

## Confirmed operations
- Match the CsvWriter operation family: Write()/WriteAsync() for headers only, single-record overloads, IEnumerable<T> overloads, and IAsyncEnumerable<T> for WriteAsync only. Return long/ValueTask<long> data-row counts, excluding headers. The user approved these operations.

## Confirmed default cell types
- Write unannotated numeric properties as numeric cells, bool properties as boolean cells, and string properties as text cells. The user approved these defaults. Large-number precision, temporal values, nulls, and annotation overrides require separate decisions.

## Confirmed numeric precision
- Throw for numeric output requiring more than 15 significant decimal digits instead of silently rounding. The user approved this rule, with an explicit text converter available to preserve such values. Exact lexical/significant-digit checks and floating-point edge cases/range still need specification.
- Reference: [Microsoft Excel specifications and limits](https://support.microsoft.com/en-us/excel/excel-specifications-and-limits).

## Confirmed custom converter output
- Write custom-converter output as literal text cells, preserving the existing span-based TryFormat contract without guessing a native cell type or interpreting formulas. The user approved this behavior. Formatting uses Text/None conversion context, as with CSV output.

## Confirmed formatting annotations
- Explicit TemporalFormat and BooleanTokens annotations select text output using the same configured culture/literal tokens as CSV. These annotations override default native output for the annotated non-null value. The user approved this behavior.

## Confirmed null output
- Write null as a blank cell unless NullToken is present, in which case write the literal token as text. Custom converters continue to own null formatting. The user approved this behavior; the blank-row restriction below applies.

## Confirmed null-token collisions
- Apply null-token collision checks to non-null text output using the configured token comparison/trimming. Native numeric/boolean/date cells do not collide with text null tokens, consistent with the XLSX reader. For example, NullToken("0") on int? permits native numeric zero, while a non-null string equal to its null token throws. The user approved this behavior.

## Confirmed entirely blank rows
- Throw when a record would serialize entirely to blank cells/empty text, so the current reader's blank-row skipping does not silently lose the record. Determine blankness after formatting; numeric zero, boolean false, non-empty text (including whitespace), and explicit null tokens count as content. The user approved this restriction.

## Confirmed DateTimeOffset output
- Write unannotated DateTimeOffset values as invariant round-trip ISO 8601 text, preserving clock time and explicit offset, consistent with CSV output and existing XLSX text reading. Do not normalize to UTC or encode as an offset-free numeric serial. The user approved this behavior.

## Confirmed DateOnly and DateTime output
- Write unannotated DateOnly/DateTime properties as native Excel serial dates with date/date-time display formats. Retain the existing requirement that DateTime.Kind is Unspecified. Explicit TemporalFormat continues to select text output. The user approved this behavior; calendar/date-system range and temporal precision require subsequent decisions.

## Confirmed date systems
- Use the 1900 date system by default, with WithDateSystem(TabularDateSystem.Excel1904) as an explicit alternative. Reuse the existing enum, accepting Excel1900/Excel1904 and rejecting None/undefined values. The user approved this behavior.
- Reference: [Microsoft Excel date systems](https://support.microsoft.com/en-us/excel/date-systems-in-excel) documents 1900 as the default in current Excel versions.

## Confirmed native date range
- Support native calendar dates from 1900-01-01 in the 1900 system or 1904-01-01 in the 1904 system, through the .NET date range's upper bound, subject to the pending temporal precision policy. Throw for earlier dates rather than silently switching cell types; an explicit TemporalFormat or custom converter can select text for them. The user approved this behavior.

## Confirmed TimeOnly output
- Write unannotated TimeOnly values as native numeric fractions of a day with a time display format, consistent with the XLSX reader's numeric time mapping. Explicit TemporalFormat continues to select text. The user approved this behavior; temporal precision remains a separate decision.

## Confirmed TimeSpan output
- Write unannotated TimeSpan as invariant constant-format text (c), consistent with CSV output, preserving negative/multi-day durations and tick precision. The user approved this behavior. Native elapsed-time output remains a future explicit option.

## Confirmed temporal precision
- Throw if a native date/time serial satisfying the numeric precision policy cannot round-trip to the original .NET ticks through the reader. Do not silently round or switch cell types. Explicit TemporalFormat/custom converters provide text output for affected values. The user approved this strict default after being told it can reject ordinary timestamps as well as high-precision values.

## Confirmed header layout
- Place headers in row 1 beginning at A1, with generated column names sorted by ordinal comparison, matching new CSV document output. Data starts in row 2. The user approved this layout. Configurable starting positions and column ordering remain future work.

## Confirmed worksheet and cell limits
- Throw when output exceeds Excel's worksheet/cell limits: 1,048,575 data rows plus one header row, 16,384 columns, or 32,767 characters in a cell. Do not truncate or automatically split output into worksheets. The user approved this behavior. Detailed text-counting/escaping and other content limits still need specification.
- Reference: [Microsoft Excel specifications and limits](https://support.microsoft.com/en-us/excel/excel-specifications-and-limits).

## Confirmed text storage
- Use inline strings for initial XLSX output, writing text with each cell rather than retaining a workbook-wide shared-string dictionary. This supports incremental bounded-memory serialization, with potentially larger files for repeated text. The user approved this approach. Shared-string output optimization remains future work.
- Reference: [Microsoft shared-string guidance](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-the-shared-string-table) confirms inline strings are valid and describes the space tradeoff.

## Confirmed stream cancellation and completion
- Match CSV stream cancellation: honor cancellation throughout async input/output, with failures/cancellation permitted to leave an incomplete, unreadable workbook in the destination. No rollback or temporary staging for supplied/factory streams; owned disposal still runs. Successful exports must finish the archive and flush before reporting success. The user approved this contract.

## Confirmed path publication
- Stage every XLSX path export in a temporary file beside its destination, including create-new output. Honor cancellation through preparation and immediately before publication; once publication starts, stop observing cancellation and report success if publication succeeds. Default publication must not overwrite a competing destination; explicit overwrite uses the approved replacement behavior. Delete owned temporary files on failure/cancellation. The user approved this contract.

## Next decision
- Recommend starting supplied-stream output at its current position without reading, seeking, rewinding, or truncating, including non-seekable streams. Treat that position as the exported archive origin. Callers prepare/reset/truncate seekable destinations as needed; leftover tail bytes can invalidate the workbook. Await user confirmation.

## Workflow
- Work on feature/xlsx-writing. Update human guides and runnable package/AOT examples when behavior is implemented; keep unfinished features on the roadmap.
- Review Rider recommendations for edited code. Markdown is exempt. Commit notes before handoff; never merge to main or enable auto-merge.
