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

## Next decision
- Recommend TabularFile.Xlsx<T>(path) returning XlsxFile<T>, a readable source that adds append/insert operations and preserves its path-specific type through fluent options, matching CsvFile<T>. Stream/factory Xlsx overloads remain read-only XlsxSource<T>; complete-document exports stay on XlsxWriter<T>. Await user confirmation.

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
