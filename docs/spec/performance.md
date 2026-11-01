# Performance and CI decisions

## Confirmed
- GitHub is the intended hosting service. Use GitHub Actions for CI.
- Runtime remains BCL-only. Benchmark and diagnostic tools are development-only.
- Performance tuning must follow evidence; buffer/pooling choices need no further user decision initially.

## XLSX stage 3
- Added an XLSX benchmark mode for numeric, inline/repeated/unique string, large-cell, disk-spill, non-seekable, and first-record workloads. Heap probes and logical temporary lengths are separate from process-wide allocation counts.
- Warm shared numeric/XML paths for at least three seconds before workload comparisons, then at least five scans and one second per case. Initial shorter warmup showed tiering effects; final raw reports use the corrected method.
- Two independent 50,000-row processes and one 200,000-row process are recorded under docs/spec/history/measurements. Keep the configurable 8 MiB default; no measured basis for changing it.
- CI uploads XLSX timing data without a time gate. Clean package consumption now compiles XLSX models externally and runs both variants, async/factories, non-seekable input, spill, and temporal mappings under JIT and AOT.
- Local checks pass. The user confirmed CI passed after commit 339dc55; stage 3 is complete. Hosted results are user-reported, not independently retrieved.

## Phase 5 implementation
- BCL harness separates parser-only, generated numeric mapping, sync/async numeric files, owned strings with quoted Unicode/multiline input, 4096-character fields, and async list/dictionary materialization.
- Minimum five warmup scans and one second per workload; raw samples and environment in JSON; process-wide allocation counters cover async continuations. Run processes sequentially without a debugger.
- Fixed-storage generated streams compare 10,000 with 1,000,000 records, both sync and async. Additional allocation must match plain model allocations within 4096 bytes. Existing parser and mapper allocation bounds remain.
- Keep 4096-character default and current per-enumeration arrays: current buffer trials show workload tradeoffs, not a consistent gain. No pooling or runtime parser changes in this slice.
- CI: Windows x64, Linux x64, macOS arm64; warning-free Release build, tests, JIT sample, project and package Native AOT publish/run, recorded benchmarks. Timing is informational, with no host-specific time gate.
- Repository published. First reported Windows failure: package-consumer publish interpreted the HTTPS --source as a local path (NU1301). Use explicit dotnet restore with .github/package-validation.nuget.config, then publish --no-restore. The config keeps source strings out of publish argument processing and resolves the local feed relative to the config file.
- The user confirmed that all three hosted jobs passed after correction 61536a1. Phase 5 is complete. This records user-reported results; the agent did not independently retrieve the private run logs. Other .NET platforms remain a compatibility requirement, not a claim of execution on all targets.
