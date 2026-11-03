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

## Next decision
- Excel documents 15-digit numeric precision. Recommend throwing for numeric output requiring more than 15 significant decimal digits instead of silently rounding; callers can explicitly supply text through a custom converter. Await user confirmation. Exact lexical/significant-digit checks, floating-point edge cases/range, and converter output behavior still need specification.
- Reference: [Microsoft Excel specifications and limits](https://support.microsoft.com/en-us/excel/excel-specifications-and-limits).

## Workflow
- Work on feature/xlsx-writing. Update human guides and runnable package/AOT examples when behavior is implemented; keep unfinished features on the roadmap.
- Review Rider recommendations for edited code. Markdown is exempt. Commit notes before handoff; never merge to main or enable auto-merge.
