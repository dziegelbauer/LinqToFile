# XLSX mutation implementation plan

Status: all three stages are implemented under [the approved contract](xlsx-mutation.md). Local solution, package, JIT/Native AOT, guide-example, allocation, and IDE reviews passed. See [validation evidence](history/validation-xlsx-mutation.md). Hosted CI is checked on the feature PR before review handoff; merging requires human approval.

## Stage 1: package validation and mutation preparation
- Build an explicit supported-feature check for Transitional/Strict packages, workbook-wide formulas/reference features, selected-sheet protection/merges, and unknown content.
- Locate the existing selected worksheet/header and validate exact schema, logical data indexes, physical placement, native syntax, shared-string references, and row limits without converting existing records through T.
- Prepare header-order/style/date-system metadata while preserving existing indexes and untouched parts. Reuse shared-string spill support and enforce resource accounting across every pass.
- Tests must cover ordinary exported and externally structured fixtures, multiple sheets, reordered headers, sparse/blank/formatted rows, protected/unsupported content, malformed input, and bounded storage.

## Stage 2: append/insert execution and public API
- Add XlsxFile<T> path-source operations and preserve its type through fluent configuration; keep stream/factory read sources unchanged.
- Implement all approved sync/async single/range overloads with one input enumeration and incremental new-row serialization/spooling.
- Rewrite only the selected worksheet and necessary supporting metadata; shift row/cell coordinates, preserve existing values/styles/parts, and add bounded required date/time styles without changing existing indexes.
- Use same-directory staged replacement, existing shared path guards, approved cancellation boundaries, and cleanup. Empty ranges validate and return zero without publication or style changes.
- Verify exception privacy/locations, failure preservation, limits, unsupported-feature rejection, async-only I/O boundaries, ownership, repeated operations, and races. Update relevant human guides/runnable examples alongside implementation.

## Stage 3: documentation, release, and review
- Review the friendly shared GitHub/NuGet introduction; update detailed mutation usage/options/limits and the unimplemented-only roadmap.
- Execute guide examples and validate the built package with a fresh cross-assembly consumer under JIT and Native AOT. Keep runtime dependencies BCL-only and avoid runtime reflection.
- Review Rider recommendations for every edited code file; Markdown/non-code files remain exempt. Record worthwhile fixes, declined material recommendations, and inspection limits.
- Complete allocation/performance checks and warning-free solution/test validation. Commit separate feature/release topics, push the feature branch, and open the PR.
- Verify Windows/Linux/macOS CI and hand off a clean, fully committed worktree. Never merge or enable auto-merge; human approval is required.
