# Phased implementation plan

Approved direction: integrate a small working vertical slice first. This plan consolidates sequencing; detailed contracts remain in the topic specifications. Do not start implementation until directed.

Every phase includes relevant human documentation using ASD-STE100, runnable-example verification, focused tests, and review of Native AOT and allocation implications. Do not claim standard conformance without verifying the authoritative reference.

## 1. Integrated foundation
Status: complete. Partial models use a generated static interface without runtime registration or reflection. The Release solution builds with zero warnings/errors; 43 tests pass. Cross-assembly JIT and Native AOT samples run through both project references and the single local package. Runtime package dependencies are empty. Human documentation and runnable examples are verified. See [phase 1 validation](../validation-phase1.md) for evidence, environment, commands, allocation baseline, and phase limits.

Establish runtime, generator, tests, benchmarks, and sample projects. Implement a narrow header-based CSV-to-model path with generated mapping and owned records.
Acceptance: a marked class with public parameterless construction, setters/init and required properties works through Csv<T>(); synchronous filtering/materialization works; an AOT sample publishes and runs; one-package generator integration is exercised. Clearly document temporarily unsupported features rather than implying full specification coverage.

## 2. Complete CSV reading
Status: complete. Phase 1 was committed as 9f8d8f5. Phase 2 adds immutable CSV options, non-seeking BOM replay, separate trimming, field/record limits, and decoded input buffering. All 220 tests pass; the Release build has no warnings/errors. Project-reference and alpha.2 package AOT samples run on macOS arm64. See [phase 2 validation](../validation-phase2.md) for evidence and phase boundaries. Public stream/async sources remain phase 4.
Implement configured encoding/BOM behavior, dialect, strict quoting, newline/blank-line handling, trimming, size limits, and structured diagnostics.
Acceptance: specification cases pass across tiny buffers, split encoding sequences, multiline fields, malformed input, non-seekable sources, and header-only files. Parser memory grows with active record size rather than file size.

## 3. Complete schema and generated mapping
Status: complete. Phase 2 was committed as cf12b87. Phase 3 implements all agreed scalar types, nullable forms, culture snapshots, reflection-free enum name parsing, temporal offset policies, and richer schema/member diagnostics. The Release build has zero warnings/errors and 323 tests pass. Conversion checks pass in Auckland and Los Angeles time zones; cross-assembly JIT and Native AOT samples run, including the alpha.3 package. See [phase 3 validation](../validation-phase3.md).
Implement annotations, inheritance diagnostics, header comparison/validation, supported types, strict conversion policies, and cross-project mappings.
Acceptance: compile-time diagnostics and runtime errors match mapping.md; no runtime reflection; retained records survive buffer reuse; conversion errors omit raw values.

## 4. Async queries and source lifetime
Status: complete. Phase 3 was committed as 2560217. Shared Sources/Reading/Queries layers and TabularRow mapping support the CSV adapter; sync/async parsing shares one state machine. All 483 tests pass, with zero Release build warnings/errors. Project-reference and alpha.4 package Native AOT samples run on macOS arm64. Sources, ownership, cancellation, materializers, and format independence are documented and tested. See [phase 4 validation](../validation-phase4.md). Timing and broader platform validation remain phase 5.
Complete source overloads, immutable configuration, independent path/factory enumerations, single-use supplied streams, async factories, cancellation, ownership, and async materialization.
Acceptance: sync/async parity; ToListAsync, ToArrayAsync, ToDictionaryAsync behavior; no blocking async-only enumeration; cleanup on completion, failure, cancellation, and early exit; race-safe single-use source claiming. Reuse BCL operators where suitable.

## 5. Performance and platform validation
Measure allocations and throughput for parsing and typed streaming; tune buffers/pooling based on evidence. Complete Windows/Linux/macOS CI and supported Native AOT execution checks.
Acceptance: reproducible benchmark baselines; representative numeric conversions avoid temporary field strings; large-file streaming memory does not grow with record count; platform and AOT checks pass.

## 6. Release preparation
Validate NuGet consumption with generator included, Apache-2.0 license and package metadata, documentation/examples, and tabular-format boundaries.
Acceptance: clean consumer and cross-project model samples work from the package; agreed license included; user/contributor documentation matches shipped behavior; deferred features are explicit.

## Future objectives
Other tabular formats; headerless files; per-column error policies; configurable missing columns; opt-in raw error values; custom converters/null tokens/exact date formats; broader model shapes and fields; custom IQueryable if justified. Writing and change tracking are outside initial scope.
