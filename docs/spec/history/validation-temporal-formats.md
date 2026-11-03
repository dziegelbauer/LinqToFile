# Exact temporal format validation

## Scope
- One TemporalFormat annotation for exact CSV/XLSX text reads and CSV output; DateTime, DateTimeOffset, DateOnly, TimeOnly and nullable forms.
- Complete dates, configured culture snapshots, existing timezone/empty/privacy rules, intentional precision omission, unchanged native workbook dates.
- Shared compile-time/runtime format analysis, LTF006 diagnostics, direct generated span-based mapping, no new runtime dependencies or reflection.

## Local evidence (2026-11-02, macOS arm64)
- Warning-free Release solution build; full suite: 1,140 passed, zero failed/skipped (baseline after the CI allocation fix: 1,064).
- Generator cases include supported standards/custom masks, nullable types, complete-date checks (weekday names do not supply day of month), literals/escapes, malformed/null/blank formats, type/timezone restrictions, offset minutes, inherited/replaced formats and cross-annotation conflicts, and setter-only reading.
- Runtime cases include exact/no-fallback parsing, trimming, culture snapshots, nullable empties, selected precision, offset/kind rules, read/write privacy/locations, existing-file byte preservation on staged write errors, limits, buffer growth, multiline Unicode/quoted format literals, intrinsic invariant standards with ar-SA, and native date/time/offset values in both Excel date systems. NUL-containing formats are compiler errors.
- Temporal runtime tests passed in separate processes with TZ=America/Los_Angeles and TZ=Pacific/Auckland.
- Warm numeric exact DateTime parsing plus formatting adds zero thread-allocated bytes in 10,000-iteration samples (minimum of three, after 20,000 warmup calls). This checks library span conversion/storage, not all BCL culture patterns or complete async operations.
- JIT sample passes with four temporal types and nullable DateTime in a separate model assembly, French text formatting, creation/append/insertion, chosen precision, and native XLSX serial dates.
- Project Native AOT publish/run with warnings as errors: passed (osx-arm64).
- Packed 0.1.0-alpha.15. Fresh external two-project consumer and cache: package assets/license/no runtime dependencies/exact README checks, JIT and Native AOT all passed, including the temporal sample.
- Repository sample restored from that local package into a separate fresh cache, Native AOT published and executed successfully.
- Ignored raw evidence: artifacts/temporal-validation.log and artifacts/temporal-final-validation.log with their exit files (0). The final pass repacks the artifact and uses another fresh package cache after the last validation change. git diff --check passes.

## Hosted review
- Existing Windows/Linux/macOS CI includes the new tests and updated JIT/project/package/external-consumer samples, plus the existing benchmark matrices.
- Hosted results belong to the PR checks. Open the PR for human review; do not merge or enable auto-merge. Main publication remains gated on a human-approved merge and successful full main CI.
