# XLSX implementation plan

Status: sequencing approved. Stage 1 is implemented and locally validated: warning-free Release build, 583 passing tests, cross-assembly JIT/Native AOT samples through both project references and the alpha.7 package, and the external clean package check. The user confirmed successful Windows/Linux/macOS CI after commit 7b798b7; stage 1 is complete. Hosted results are user-reported, not independently retrieved. See ../validation-xlsx-stage1.md. Stage 2 is authorized and implemented; local validation passes: 688 tests, warning-free Release build, project/package JIT and Native AOT, and clean package consumption; the user confirmed CI passed after commit 32ff4d2, completing stage 2. Stage 3 has not started. See ../validation-xlsx-stage2.md.

## 1. Integrated foundation

Build a working path from TabularFile.Xlsx<T>() through the existing generated mapper and shared query engine.
Use paths and seekable streams initially, with synchronous enumeration, worksheet/header selection, package relationships, Strict/Transitional namespaces, sparse cells, and inline text/native numeric/boolean mapping.
Include merge preflight, structured XLSX locations, and required-part/XML validation from the start so the slice does not silently accept unsupported table structures.
Create small BCL-generated test packages, a cross-assembly executable sample, and project/package Native AOT checks.

Acceptance: selected-sheet typed LINQ works; existing CSV tests pass unchanged; diagnostics distinguish cell coordinates from logical records; no runtime dependencies/reflection; package and AOT integration work. Explicitly reject temporarily unsupported cell/features rather than silently returning incorrect data. Document slice limitations and do not call this complete XLSX support.

## 2. Complete cells and storage

Implement shared strings with bounded memory and disk spill/indexing; rich text and escapes; cached formulas; all agreed numeric and temporal conversions, null/empty/type boundaries, trimming/culture, and resource limits.
Cover shared strings and formulas in headers, malformed typed content in unmapped columns, metadata/merge edge cases, temporal boundaries, and no-temp-file behavior.
Confirm required markup handling and compatibility with both XLSX variants. Verify independent fixtures from real spreadsheet producers when available, with provenance and redistribution rights recorded.

Acceptance: agreed cell and conversion matrices pass; 1900/1904 and rounding boundaries are explicit; no temporary strings for representative native numeric conversion; shared-string text/index storage respects its accounting budget; all configured limit boundaries throw accurately. No whole-sheet materialization, formula evaluation, or display-format rendering.

## 3. Async sources, performance, and release validation

Complete non-seekable input spooling, sync/async factories, genuine async I/O, cancellation throughout preprocessing and reading, source claiming, cleanup, and every materializer.
Measure numeric/string-heavy workbooks, high-cardinality shared strings, large cells, preflight startup, small budgets, and disk spill. Separate fixed metadata costs, per-record allocations, retained memory, and temporary-storage use.
Extend Windows/Linux/macOS CI and the clean external package consumer to exercise XLSX under JIT and Native AOT. Update STE100 human documentation, package README, examples, option/error/conversion references, and validation records.

Acceptance: sync/async parity; concurrent path/factory enumerations are independent; no-temp mode and resource limits work; cancellation/early exit release owned resources; CSV regressions absent; cross-platform tests, clean package consumption, and AOT samples pass. Document any unavoidable synchronous ZIP metadata/seek operations accurately. The feature is complete only when all agreed behavior and hosted checks pass.

## Workflow

- Implement and review one stage at a time after authorization.
- Resolve technical uncertainties with narrow probes before broad changes; ask the user if a probe exposes a required behavior change.
- Keep test fixtures small and deterministic. Do not introduce a spreadsheet runtime dependency to create them.
- Each stage includes human documentation, focused correctness tests, and allocation/AOT review.
- Public package publication and release creation remain separate decisions.
