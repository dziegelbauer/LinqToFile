# Scope and roadmap

## Confirmed
- C# library for LINQ over tabular files, starting with CSV.
- Next format: XLSX. Stage 2 adds shared strings, rich text, cached formulas, native temporal conversions, and resource limits to synchronous paths/seekable streams. Local validation passes; the user confirmed CI passed after commit 32ff4d2, completing stage 2. Stage 3 adds sync/async factories, non-seekable spooling, async I/O/cancellation, and performance validation. Its local checks pass; hosted CI is pending. See xlsx.md and xlsx-plan.md.
- Keep source lifetime, shared queries, schema binding, and mapping separate from individual format readers. CSV code lives under Formats/Csv; shared layers stay reusable for future tabular formats within the single runtime package.
- Minimum target: .NET 10, compatible with later versions.
- Support every platform supported by the target .NET runtime.
- Production library dependencies: BCL only.
- Distribute one consumer NuGet package containing the BCL-only runtime and build-time source generator; no separate generator installation or manual registration is required.
- Initial library is read-only. File writing and change tracking are outside first-release scope.
- Release license: Apache License 2.0 (SPDX identifier Apache-2.0).
- Source-generator tooling may use compiler dependencies separately from the runtime library.
- Development-only tests and benchmarks may use external packages.
- Fluent configuration is immutable: options return a new configuration without changing existing queries.
- EF Core-like fluent configuration, including WithEncoding.
- Strongly typed materialization, incremental record streaming, and full collections.
- Prioritize efficiency and avoid unnecessary allocations.
- Native AOT compatibility is a top-level requirement; designs and validation must account for it.
- Minimizing allocations is a top-level requirement across parsing, mapping, streaming, and materialization.
- First-release validation includes publishing and running a small Native AOT sample and allocation benchmarks for parsing and typed streaming.
- Cross-platform validation covers Windows, Linux, and macOS. Include Native AOT publish/run checks on supported CI targets, plus non-seekable-stream and tiny-read-buffer tests.
- Build and maintain human-readable documentation within the repository using ASD-STE100 (Simplified Technical English). Agent planning notes are separate from human-facing documentation.
- Each implementation slice must update relevant documentation and verify runnable examples. Coverage includes getting started, API/options and annotations, conversions/errors, streaming/ownership, Native AOT, and contributor architecture.
- Plan first; implement in small, user-directed steps.

## Future objectives
- Platform-sized nint/nuint conversions.
- Other tabular formats.
- Headerless CSV.
- Configurable per-column error handling through annotations.
- Configurable missing-column handling (initially always throws).
- Opt-in raw field values in errors through a sensitive-data diagnostic configuration.
- Custom IQueryable provider, if a concrete need emerges.

## Approved planning direction
- Consolidate agreed requirements into a phased plan with acceptance criteria before implementation.
- First implementation slice: solution structure, generator/runtime integration, a small CSV-to-model vertical slice, tests, an AOT sample, and matching documentation. Expand CSV features after this integration is validated.
- Subsequent phases cover complete CSV parsing, mapping/conversion behavior, sync/async queries and resource lifetime, performance, extensibility, and packaging. Acceptance criteria are recorded in plan.md.
- Include the canonical Apache-2.0 license text and corresponding package license metadata during project setup/packaging. Use accurate attribution; do not invent a copyright holder.
