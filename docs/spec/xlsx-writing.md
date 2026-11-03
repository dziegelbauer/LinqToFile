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

## Next decision
- Recommend create-new-only behavior for path destinations: throw if the target exists. Replacing an existing workbook would require an explicit future option/API, consistent with the requirement that file creation and mutation communicate intent.

## Workflow
- Work on feature/xlsx-writing. Update human guides and runnable package/AOT examples when behavior is implemented; keep unfinished features on the roadmap.
- Review Rider recommendations for edited code. Markdown is exempt. Commit notes before handoff; never merge to main or enable auto-merge.
