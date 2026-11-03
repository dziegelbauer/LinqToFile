# Headerless CSV reading

## Confirmed contract
- The user authorized specification recording and implementation on a feature branch.
- CSV only. Headers remain the default; XLSX requires headers.
- WithColumnNames(params string[] names) selects supplied-name mapping. Copy the non-null, nonempty list immediately. Existing header trimming/comparison/name validation and missing-column rules apply. Every data record must contain exactly the supplied number of fields, including unmapped fields.
- WithColumnIndexes() selects generated model indexes. WithHeaders() restores header-based reading. Each selection replaces the previous selection in a new immutable configuration.
- ColumnIndexAttribute(int index) uses zero-based positions. Every mapped property must have an index when index mapping is selected; validate completeness immediately in WithColumnIndexes(), without opening input. Missing indexes are configuration errors, not compiler errors, so named models remain valid.
- Negative or duplicate effective indexes are compiler errors. Gaps and unmapped trailing fields are allowed; skipped fields retain syntax and size-limit validation.
- The first data record establishes index-mode field count and must contain every mapped index. Later records must match that count.
- Empty input, including only skipped blank lines, returns an empty sequence. Configuration and model validation still apply.
- Column and ColumnIndex may coexist; each mode uses its matching annotation. The nearest ColumnIndex in an override chain wins; effective Ignore/ColumnIndex conflicts are compiler errors.
- Adding ColumnIndex alone does not select index mapping. It has no effect on XLSX reads or CSV/XLSX output ordering. Headerless writing remains deferred.
- First data record is logical record 1. Physical line numbering, multiline parsing, limits, conversions, privacy, cancellation, streaming, and source lifetime retain existing behavior.
- Paths, supplied streams, synchronous factories, and asynchronous factories support both modes and existing sync/async query operations.

## Implementation and validation
- Extend generated mapper metadata with optional indexes, without reflection or per-record metadata allocation. Preserve compatibility with older mappers through a virtual default.
- Reuse shared schema/query layers; expose configuration only on CSV sources. CSV path write operations keep existing header-based contracts.
- Validate generator diagnostics/inheritance, configuration transitions/snapshots, first-record handling, widths, empty input, skipped-field validation, diagnostics, sync/async parity, lifetime/cancellation, native XLSX behavior, and unchanged writing.
- Add human usage documentation and runnable cross-assembly JIT/Native AOT examples. Inspect edited code in Rider, run relevant full/package checks, commit all work, and open a PR for human review. Never merge or enable automatic merging.
