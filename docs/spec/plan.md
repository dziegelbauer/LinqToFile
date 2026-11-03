# Phased implementation plan

Approved direction: integrate a small working vertical slice first. This plan consolidates sequencing; detailed contracts remain in the topic specifications. Do not start implementation until directed.

Every phase includes relevant human documentation using ASD-STE100, runnable-example verification, focused tests, and review of Native AOT and allocation implications. Do not claim standard conformance without verifying the authoritative reference.

## 1. Integrated foundation
Status: complete. Partial models use a generated static interface without runtime registration or reflection. The Release solution builds with zero warnings/errors; 43 tests pass. Cross-assembly JIT and Native AOT samples run through both project references and the single local package. Runtime package dependencies are empty. Human documentation and runnable examples are verified. See [phase 1 validation](history/validation-phase1.md) for evidence, environment, commands, allocation baseline, and phase limits.

Establish runtime, generator, tests, benchmarks, and sample projects. Implement a narrow header-based CSV-to-model path with generated mapping and owned records.
Acceptance: a marked class with public parameterless construction, setters/init and required properties works through Csv<T>(); synchronous filtering/materialization works; an AOT sample publishes and runs; one-package generator integration is exercised. Clearly document temporarily unsupported features rather than implying full specification coverage.

## 2. Complete CSV reading
Status: complete. Phase 1 was committed as 9f8d8f5. Phase 2 adds immutable CSV options, non-seeking BOM replay, separate trimming, field/record limits, and decoded input buffering. All 220 tests pass; the Release build has no warnings/errors. Project-reference and alpha.2 package AOT samples run on macOS arm64. See [phase 2 validation](history/validation-phase2.md) for evidence and phase boundaries. Public stream/async sources remain phase 4.
Implement configured encoding/BOM behavior, dialect, strict quoting, newline/blank-line handling, trimming, size limits, and structured diagnostics.
Acceptance: specification cases pass across tiny buffers, split encoding sequences, multiline fields, malformed input, non-seekable sources, and header-only files. Parser memory grows with active record size rather than file size.

## 3. Complete schema and generated mapping
Status: complete. Phase 2 was committed as cf12b87. Phase 3 implements all agreed scalar types, nullable forms, culture snapshots, reflection-free enum name parsing, temporal offset policies, and richer schema/member diagnostics. The Release build has zero warnings/errors and 323 tests pass. Conversion checks pass in Auckland and Los Angeles time zones; cross-assembly JIT and Native AOT samples run, including the alpha.3 package. See [phase 3 validation](history/validation-phase3.md).
Implement annotations, inheritance diagnostics, header comparison/validation, supported types, strict conversion policies, and cross-project mappings.
Acceptance: compile-time diagnostics and runtime errors match mapping.md; no runtime reflection; retained records survive buffer reuse; conversion errors omit raw values.

## 4. Async queries and source lifetime
Status: complete. Phase 3 was committed as 2560217. Shared Sources/Reading/Queries layers and TabularRow mapping support the CSV adapter; sync/async parsing shares one state machine. All 483 tests pass, with zero Release build warnings/errors. Project-reference and alpha.4 package Native AOT samples run on macOS arm64. Sources, ownership, cancellation, materializers, and format independence are documented and tested. See [phase 4 validation](history/validation-phase4.md). Timing and broader platform validation remain phase 5.
Complete source overloads, immutable configuration, independent path/factory enumerations, single-use supplied streams, async factories, cancellation, ownership, and async materialization.
Acceptance: sync/async parity; ToListAsync, ToArrayAsync, ToDictionaryAsync behavior; no blocking async-only enumeration; cleanup on completion, failure, cancellation, and early exit; race-safe single-use source claiming. Reuse BCL operators where suitable.

## 5. Performance and platform validation
Status: complete. Phase 5 work was committed as 0c9301e; the Windows package-restore correction was committed as 61536a1. The user confirmed that all three GitHub Actions jobs passed after the correction. The matrix covers Windows x64, Linux x64, and macOS arm64, including project and package Native AOT publish/run checks. Local validation includes all 485 tests, a warning-free Release build, reproducible benchmark reports, buffer trials, a sampling profile, and million-row allocation checks. See [phase 5 validation](history/validation-phase5.md). Keep the 4096-character default and current arrays; no runtime performance changes were justified.
Measure allocations and throughput for parsing and typed streaming; tune buffers/pooling based on evidence. Complete Windows/Linux/macOS CI and supported Native AOT execution checks.
Acceptance: reproducible benchmark baselines; representative numeric conversions avoid temporary field strings; large-file streaming memory does not grow with record count; platform and AOT checks pass.

## 6. Release preparation
Status: complete. Phase 6 work was committed as d8c7030. The user confirmed that CI passed, including the expanded consumer check on the Windows/Linux/macOS matrix. Alpha.6 metadata, a NuGet-facing README, an external two-project consumer check, and release documentation are prepared. The clean consumer passes JIT and Native AOT, including the exact packaged README examples. See [phase 6 validation](history/validation-phase6.md). No public package publication or release creation is authorized by this phase.
Validate NuGet consumption with generator included, Apache-2.0 license and package metadata, documentation/examples, and tabular-format boundaries.
Acceptance: clean consumer and cross-project model samples work from the package; agreed license included; user/contributor documentation matches shipped behavior; deferred features are explicit.

## Next format: XLSX
Behavior and [implementation stages](xlsx-plan.md) are approved. Stage 1 is implemented and locally validated with 583 passing tests and project/package Native AOT samples; the user confirmed successful CI after commit 7b798b7, completing stage 1. Stage 2 is authorized and implemented; local validation passes: 688 tests, warning-free Release build, project/package JIT and Native AOT, and clean package consumption; the user confirmed CI passed after commit 32ff4d2, completing stage 2. Stage 3 is authorized and locally validated with 719 tests, project/package JIT and Native AOT, clean consumption, and recorded performance workloads. The user confirmed CI passed after commit 339dc55, completing stage 3; see [stage 3 validation](history/validation-xlsx-stage3.md). See [stage 2 validation](history/validation-xlsx-stage2.md). The approved XLSX subset is implemented; all three stages are complete. See [XLSX planning](xlsx.md) and [stage 1 validation](history/validation-xlsx-stage1.md).

## Future objectives
Other tabular formats; per-column error policies; configurable missing columns; opt-in raw error values; multiple/separate token and temporal input/output forms; broader model shapes and fields; custom IQueryable if justified. Change tracking remains outside current scope.

## Headerless CSV

The user approved supplied column names and zero-based ColumnIndex model mapping for CSV reads. The complete contract is in [headerless CSV](headerless-csv.md). Reuse shared schema/query layers and validate all source types, sync/async behavior, generator diagnostics, streaming allocations, and packaged Native AOT examples.

## Per-column converters

Implement the approved static span-based parsing/formatting contract for shared CSV/XLSX reads and CSV writes. Preserve unannotated behavior and compiler-check converter compatibility. See [converter contract](converters.md) for confirmed native-cell, nullable, and failure behavior and [validation](history/validation-converters.md) for evidence.

## Next capability: CSV writing

The user has confirmed creation, append, and positional insertion contracts in [writing.md](writing.md).
The [writing implementation plan](writing-plan.md) has four stages: serialization/path API/creation; append and quick append; positional insertion; performance and release validation.
Stage 1 is complete locally: 781 tests, warning-free Release build, project/package Native AOT, and clean JIT/Native AOT consumption on macOS arm64. Hosted platform validation remains pending CI. See [stage 1 validation](history/validation-writing-stage1.md).
Stage 2 is also complete locally: 898 tests, warning-free Release build, project/package Native AOT, and clean JIT/Native AOT consumption on macOS arm64. See [stage 2 validation](history/validation-writing-stage2.md). Hosted checks remain pending CI.
Stage 3 is complete locally: 1,024 tests, warning-free Release build, project/package Native AOT, and clean JIT/Native AOT consumption on macOS arm64. See [stage 3 validation](history/validation-writing-stage3.md). Hosted checks remain pending CI.
Stage 4 is complete: performance measurements/tuning, a shared GitHub/NuGet introduction, 1,025 tests, and the full Windows/Linux/macOS matrix passed in [run 41468149136](https://github.com/dziegelbauer/LinqToFile/actions/runs/41468149136) at commit 2214df5. See [stage 4 validation](history/validation-writing-stage4.md). The writing feature is ready for a pull request and human review; never merge it automatically. Keep unimplemented behavior on the public roadmap.
