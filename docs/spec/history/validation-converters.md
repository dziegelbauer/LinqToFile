# Custom converter validation

## Scope
- Per-property ColumnConverter annotation and IColumnConverter<T> static span-based parsing/formatting.
- Complete property types, including nullable values and nullable reference annotations, scalar overrides, and custom domain types.
- Shared CSV/XLSX reads and CSV writes, with native cell kind/culture/workbook date-system context.
- Direct generated calls without runtime reflection, registration, converter instances, or added runtime dependencies.

## Local evidence (2026-11-02, macOS arm64)
- Release solution build with warnings as errors: zero warnings/errors.
- Full suite: 1,064 passed, zero failed/skipped (baseline 1,025).
- Generator checks: direct static dispatch; explicit interface implementations; closed generics; private constructors; setter-only reads; inherited/replaced converters; Ignore conflicts; mismatched types/nullability; abstract/open/inaccessible/file-local converters, including file-local containers.
- Runtime checks: culture snapshots, trimming, empty/null input and nullable output, owned reference values, CSV quoting/custom separator/quote, append/insert, private conversion/write failures with reordered columns and XLSX locations, reusable output growth, invalid output counts, field/record limits, both workbook date systems and native kinds.
- Warm scalar parsing plus formatting (10,000 iterations after 20,000 warmup calls) adds zero thread-allocated bytes. This measures library dispatch/storage and the span-based Amount converter, not every user converter or whole-file async I/O.
- JIT sample runs pass, including cross-assembly ProductCode, class converter with private constructor/explicit static methods, and nullable count conversion.
- Project Native AOT publish/run (osx-arm64), warnings as errors: passed.
- Packed 0.1.0-alpha.14; fresh external two-project consumer with a fresh package cache: metadata/license/zero runtime dependencies/exact README checks, JIT and Native AOT all passed.
- Repository package consumer restored from local feed with a separate new cache, then Native AOT published and executed: passed.
- Ignored raw logs: artifacts/converters-final-validation.log and artifacts/converters-final-validation.exit (0). git diff --check passes.

## Hosted validation
- The existing Windows x64, Linux x64, and macOS arm64 CI matrix runs the new tests and updated samples/package verification, including converter cases, without a separate workflow path.
- Hosted results will be recorded after pushing the feature branch. No branch or PR publication occurs; NuGet publication remains gated on a human-approved merge and successful main CI.
- Never merge the PR or enable auto-merge. Keep the worktree clean at handoff.
