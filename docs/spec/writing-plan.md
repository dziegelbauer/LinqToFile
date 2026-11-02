# CSV writing implementation plan

Status: all four stages are complete for the confirmed contracts in [writing.md](writing.md).
All 1,025 tests, project/package Native AOT, clean consumers, and the 93-case writing matrix pass on Windows x64, Linux x64, and macOS arm64 in [run 41468149136](https://github.com/dziegelbauer/LinqToFile/actions/runs/41468149136) for commit 2214df5.
See [stage 1](history/validation-writing-stage1.md), [stage 2](history/validation-writing-stage2.md), [stage 3](history/validation-writing-stage3.md), and [stage 4 validation](history/validation-writing-stage4.md).
The feature branch is committed/pushed; final documentation/benchmark-harness follow-up receives the normal PR checks. Main merges remain human-approved only.
Keep unrelated publication-verification notes separate when committing implementation work.

## 1. Serialization, path API, and creation

Run narrow probes first for byte boundaries/encoding, filesystem publication, temporal read-back, and the proposed in-place insertion mechanism.
Raise any required contract change with the user before relying on it.

Introduce CsvFile<T> for path sources while preserving existing read usage and fluent typing.
Keep stream/factory sources read-only and validate compiler/API compatibility with existing generic helpers.
Add a format-neutral generated serialization boundary, isolated from CSV escaping and file mutation.
Use direct generated property access and enum formatting, with no runtime reflection or runtime packages.

Implement Create/CreateAsync with headers only or initial IEnumerable<T>/IAsyncEnumerable<T> input.
Include temporary-file publication and explicit in-place creation, non-overwrite behavior, cancellation boundaries, and cleanup.
Implement agreed headers, dialect, encoding, line endings, scalar formats, nulls, output limits, row counts, and errors.
Update temporal reader compatibility for writer formats without discarding existing kind/offset rules.

Acceptance: deterministic headers; complete scalar/nullable matrix; custom delimiter/quote and encoding round trips; non-Gregorian temporal tests; bounded incremental input; existing-file rejection; failure/cancellation behavior; compile-time read/write capability separation; existing CSV/XLSX reads pass.
Include a cross-assembly JIT/package/Native AOT creation sample and usage documentation for the APIs implemented in this stage.

## 2. Append, validation, and quick append

Stage 2 is complete locally. The user approved restricting append to UTF-8/16/32 and stateless single-byte encodings, retaining other encoding support for reads and creation. Validation evidence is linked above.

Implement Append/AppendAsync and AppendRange/AppendRangeAsync for both write modes.
Bind output to exact existing headers in file order; reject missing destinations and invalid headers.
Validate all existing CSV syntax/widths by default without model conversion.
Implement WithAppendValidation(false), retaining header/schema/new-row checks.
Preserve existing bytes/BOM and infer new-row endings from the header, with LF fallback and typed override.
Handle missing final terminators, single-column empty records, empty ranges, single-use sequence enumeration, and process-local overlapping writes.

Acceptance: both modes and both input sequence types; byte-for-byte unchanged existing content; custom dialects and split encoding sequences; malformed existing data and quick mode distinction; no destination changes for empty ranges; original unchanged on pre-commit failures in default mode; in-place cancellation cutoff and accepted partial failures.
Extend JIT/package/Native AOT samples and human documentation with append behavior.

## 3. Positional insertion

Stage 3 is complete locally. It uses the same existing-file encoding boundary as append for consistent byte-preserving mutation. Decoded record starts track original bytes without re-encoding old fields. Validation evidence is linked above.

Implement Insert/InsertAsync and InsertRange/InsertRangeAsync using long, zero-based logical data-record indexes.
Exclude headers and skipped blank lines from indexing; preserve blank-line placement and multiline records.
Support both temporary-file replacement and bounded-memory in-place insertion without hidden disk spill or repeated input enumeration.
Always validate existing syntax and widths before destination mutation, independently of the append-validation setting.

Acceptance: beginning/middle/end/header-only positions; negative/out-of-range indexes; Unicode/BOM and mixed existing line endings; quoted multiline values; empty ranges; large suffixes and incremental input; exact existing-byte preservation; cancellation and fault injection before/after each mutation boundary; temporary-file cleanup and write-guard release.
Extend the cross-assembly and package samples to exercise both insertion modes.

## 4. Performance and release validation

Stage 4 is complete. The user requires a pull request to main after committing/pushing. All merges require human approval; the agent must not merge or enable automatic merging.

Measure creation, validated/quick append, and positional insertion with both mutation modes and sync/async execution.
Separate new-row formatting allocation, input enumeration, existing-file scan/copy costs, suffix movement, retained buffers, and temporary disk use.
Use numeric/string-heavy inputs, large fields, long files, tiny read boundaries, and streamed ranges.
Verify Native AOT, clean external package consumption, and the Windows/Linux/macOS CI matrix.

Acceptance: no runtime dependencies/reflection; no full-input materialization; bounded storage apart from active records/results; meaningful allocation checks; reviewed throughput tradeoffs; all existing tests and runnable examples pass.
Human docs describe current usage only. Keep validation evidence in agent records and unimplemented writing features on the roadmap.
After all writing stages are complete, review the main README and NuGet package information as user introductions.
The user requires a clear explanation of why to use the library and what it can do, installation instructions,
quick examples, and links to detailed guides. Keep these pages focused on user value and current capabilities.

## Delivery

- Complete one stage at a time and keep unsupported APIs out of the public surface until implemented.
- Every stage includes relevant tests, STE100 usage documentation, and allocation/AOT review.
- Use feature branches and pull requests. Successful main CI automatically publishes a prototype prerelease.
- Updates, deletes, tracking, writable streams/factories, and XLSX writing remain outside this CSV-writing implementation.
