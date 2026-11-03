# XLSX append and insertion design

## Authorized direction
- The user selected XLSX append and insertion next to bring row-operation parity with CSV. Defer the suggested multiple temporal input/output formats; that item stays on the roadmap.
- PR #9 is merged. Start this work from updated main on feature/xlsx-row-mutation.
- Preserve .NET 10+, cross-platform support, Native AOT, a BCL-only runtime, no runtime reflection, and minimized allocations.
- Build the contract with the user one decision at a time. The recommendations below are proposals until approved; do not implement unresolved public behavior.

## Proposed parity
- Reuse CSV operation names: Append/AppendRange, Insert/InsertRange, and asynchronous variants accepting sync/async sequences as applicable. Preserve long data-row indexes/counts and explicit existing-file intent.
- Keep new-workbook exports on XlsxWriter<T>; define the existing-workbook mutation entry point separately while retaining consistent source/configuration conventions.
- Prefer same-directory staging and replacement, incremental processing, existing generated cell serialization/annotations, private worksheet/cell-aware errors, and current human documentation/runnable samples.

## Confirmed destinations
- Support existing file paths only for initial XLSX mutation, matching CSV append/insert. The user approved this scope. Supplied-stream/factory mutation remains future work; existing complete-document stream exports stay available.

## Confirmed public entry point
- TabularFile.Xlsx<T>(path) returns XlsxFile<T>, a readable source that adds append/insert operations and preserves its path-specific type through fluent options, matching CsvFile<T>. Stream/factory Xlsx overloads remain read-only XlsxSource<T>; complete-document exports stay on XlsxWriter<T>. The user approved this API direction.

## Confirmed operation family
- Provide Append/AppendRange and Insert/InsertRange, with corresponding async variants. Single-record and IEnumerable<T> inputs support sync/async methods; IAsyncEnumerable<T> ranges support async methods only. Return long/ValueTask<long> inserted-row counts; async methods accept an optional CancellationToken. The user approved this CSV-parity method family.

## Confirmed publication and cancellation
- Require an existing workbook and use staged replacement only initially: prepare changes in a temporary file beside it, and replace only after successful completion. Honor cancellation until immediately before replacement; once replacement starts, report success if it succeeds. The user approved this contract. Do not expose in-place XLSX mutation initially.

## Confirmed worksheet selection
- Match XLSX read selection: mutate the first worksheet in workbook order by default, or the existing sheet selected with WithWorksheet(name). Match names ordinal-ignore-case and throw when missing; never create a missing worksheet implicitly. The user approved this behavior. Existing read selection includes hidden/very-hidden worksheets; visibility does not change the selected worksheet.

## Confirmed insertion indexes
- Use zero-based long insertion indexes counting logical data records returned under the selected header/blank-row rules, excluding the header and skipped blank rows. Zero inserts before the first data record; the existing data-record count appends. Negative or greater indexes throw. WithHeaderRow retains its current physical one-based meaning. The user explicitly confirmed this contract after checking CSV parity. Exact physical placement and preservation of blank/formatted rows remain separate decisions.

## Confirmed physical placement
- Insert new physical rows immediately before the indexed data row, keeping preceding blank rows in place and shifting that row and subsequent worksheet rows down by the inserted count. Append (including Insert at the data-record count) goes after the last existing physical row, including trailing blank/formatted row elements, rather than filling gaps. A declared dimension alone must not create artificial trailing rows. Preserve existing blank/formatted row elements rather than consuming them as insertion space. The user approved this behavior; supported reference-bearing features still require separate scope decisions.

## Confirmed existing-workbook scope
- Initially support plain tabular workbooks: ordinary values, shared/inline strings, cell styles, and multiple worksheets. Preserve supported existing content. Reject workbooks with formulas or reference-bearing features (such as tables, named ranges, charts, or drawings) that the implementation cannot reliably update/preserve. Do not rebuild arbitrary workbooks through typed read/write mapping or silently strip unsupported features. The user approved this initial scope; exact supported-feature validation and package preservation rules require subsequent specification.

## Confirmed preservation
- Preserve untouched worksheet/part payloads without rewriting their XML; modify only the selected worksheet and supporting metadata strictly required by the mutation. Preserve existing cell values/types/styles, worksheet names/order/visibility, shared-string indexes, and workbook date system. ZIP compression/container bytes may change, so do not promise byte-identical archive output. The user approved these preservation rules; new-row styles and supporting metadata still require separate decisions.

## Confirmed new-row formatting
- Format new rows with the existing XLSX writer's native/text/annotation and strict precision rules, using the existing workbook's date system. Do not copy adjacent row/cell formatting automatically. Reuse compatible native date/time styles or add bounded required styles without changing existing style indexes; leave general style inheritance/configuration for future work. The user approved these rules.

## Confirmed header/schema matching
- Require exact header/model matching for mutation, as with CSV writes: all mapped columns must exist, no extra header columns are permitted, and duplicate/invalid headers throw under configured comparison/trimming. Preserve existing header order and write new cells in that order rather than sorting or rewriting the header. Reading retains its existing extra-column behavior. The user approved these rules.

## Confirmed existing-data validation
- Validate the selected worksheet's existing structure and native cell syntax before publication, without converting existing data through T or invoking its converters. Preserve existing values even when they would not convert to the current model. Missing cells keep existing XLSX empty-cell rules; malformed coordinates, invalid shared-string references, spreadsheet error cells, or non-empty data beyond the header range throw. Keep this validation mandatory for both append and insertion initially, without a quick-append bypass. The user approved these rules; workbook-wide unsupported-feature checks remain a separate scope decision.

## Confirmed empty ranges
- Return zero and leave the workbook unchanged for empty ranges, while still validating existence, selected worksheet, headers/schema, insertion index, mandatory existing-data validation, and supported workbook scope. Do not publish a replacement or add styles for empty input. The user approved this CSV-parity behavior.

## Next decision
- Recommend workbook-wide rejection of formulas and unsupported reference-bearing features, including on unselected sheets, because they can reference rows in the selected sheet. Check all worksheets and relevant workbook/package metadata before publication. Ordinary value conversion/structural table validation remains focused on the selected worksheet; these broader scans detect unsupported features, not mapped data errors on unrelated sheets. Await user confirmation.

## Remaining design topics
- Public path-source/mutation API and worksheet selection.
- Supported existing workbook content, preservation of unrelated parts, and rejection of features whose references cannot be updated correctly. Do not promise transparent insertion into arbitrary formula/table/chart-rich workbooks.
- Header/data-row boundaries, logical indexes versus physical worksheet rows, and sparse/blank-row behavior.
- Existing schema validation, column order, native/text cells, styles/shared strings, date-system preservation, limits, and unsupported package variants.
- Reference/coordinate updates, cancellation/publication guarantees, empty input, concurrency, and whether any CSV performance options are appropriate for ZIP-based mutation.
- Implementation stages, cross-platform/package/Native AOT checks, documentation, and review handoff.

## Workflow
- Commit notes before handoff and keep the worktree clean. Review Rider recommendations for edited code; Markdown is exempt.
- Complete work through a feature PR. All merges to main require human approval; never merge or enable auto-merge.
