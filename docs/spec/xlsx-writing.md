# XLSX writing design

## Confirmed
- The user approved a separate TabularFile.XlsxWriter<T>() API and requires consistent API design across file types. Follow the CsvWriter<T> naming and immutable configuration conventions where applicable.
- Preserve .NET 10+, cross-platform support, Native AOT, BCL-only runtime, no runtime reflection, and minimized allocations.
- Plan behavior with the user one decision at a time before implementation. A suggestion is not a confirmed contract.

## Proposed initial scope
- Export new workbooks with one named worksheet, headers, and typed cells using generated mappings.
- Provide Write/WriteAsync for header-only, single-record, synchronous sequences, and asynchronous sequences where applicable; return data-row counts consistent with CSV exports.
- Support paths and supplied streams; consider the existing sync/async factory pattern for parity.
- Defer append, insertion, and editing existing workbooks.

## Confirmed existing-file behavior
- Add the typed option WithExistingFileBehavior(ExistingFileBehavior.Overwrite), with ExistingFileBehavior.Throw as the default. The user approved intentional overwrite for XLSX path exports and the same option for CSV Create/CreateAsync.
- Overwrite prepares complete output in a temporary file and replaces the destination only after output succeeds, so export failures preserve the original. This applies to complete-document path output; CSV append/insert behavior stays unchanged. Supplied streams retain their separate current-position/partial-output contract.
- This is an approved planned API, not implemented behavior. Update human guides only when implementation is available.
- The user approved rejecting CSV creation when both WithInPlaceWrites() and ExistingFileBehavior.Overwrite are selected. Do not silently weaken staged overwrite protection. This restriction does not change append/insert behavior.

## Confirmed worksheet scope
- Export one worksheet per workbook initially, named Sheet1 by default. WithWorksheet(string name) selects its output name. The user approved this scope; multiple worksheets remain future work.

## Next decision
- Recommend supporting paths, supplied writable streams, synchronous stream factories, and asynchronous stream factories in the initial XLSX writer. Mirror the CSV stream ownership, shared single-use allowance, and repeatable/concurrent independent factory contracts. Await user confirmation.

## Workflow
- Work on feature/xlsx-writing. Update human guides and runnable package/AOT examples when behavior is implemented; keep unfinished features on the roadmap.
- Review Rider recommendations for edited code. Markdown is exempt. Commit notes before handoff; never merge to main or enable auto-merge.
