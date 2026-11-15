# Performance and CI decisions

## Confirmed
- GitHub is the intended hosting service. Use GitHub Actions for CI.
- Runtime remains BCL-only. Benchmark and diagnostic tools are development-only.
- Performance tuning must follow evidence; buffer/pooling choices need no further user decision initially.

## Main CI allocation guard fix after PR #4
- Main run 41553302441 failed only the Windows NumericAndEnumMappingAddsNoPerFieldStringAllocations test: 2,768 excess bytes versus its 512-byte bound. Linux/macOS passed; publication was skipped. The preceding feature and PR matrices had passed.
- The failing guard warmed only 100 calls and measured once. Align it with existing allocation guards: warm both mapper/model paths for 20,000 calls, then use the minimum of three 10,000-row samples for each path. Keep the existing 0..512-byte bound. Real per-field allocations recur in every sample.
- Apply repeated sampling to the new scalar converter guard as well, retaining its zero-byte bound. No production code, dependencies, or publishing gates change.
- Local validation: warning-free Release solution build, all 1,064 tests, and five fresh test-process runs of both allocation guards pass. A temporary field-string allocation in each measured iteration failed at 320,000 excess bytes against the same 512-byte limit; that mutation was removed and the full suite rerun successfully.
- The user explicitly paused feature/temporal-formats, approved the annotation conflict rule there, and instructed this CI fix to be merged after validation, then to return to that branch. This is explicit authorization for this fix's merge only; other merges still require human approval.

## CSV writing stage 4
- The writing harness isolates formatting, input enumeration, validation, byte-offset scans, copying, rotation, and end-to-end operations. Its 93-case matrix includes both mutation modes and execution styles plus single-call/range comparisons. Timings are informational.
- Five local reports cover baseline/tuning, one-million-row scale, tiny read buffers, retained heap/temporary snapshots, and short batches. The UTF-8 ASCII byte-position path improves measured scan time without changing fallback/offset semantics. Keep current buffer sizes and movement blocks; no further pooling changes are justified.
- Numeric formatting allocates no per-row storage for a reused model; old-row scan and new-row allocation guards pass. Async I/O allocations are reported separately from retained heap. See [stage 4 validation](history/validation-writing-stage4.md) for scope, raw reports, caveats, and hosted acceptance.
- Hosted run 41468149136 at 2214df5 passed all three platform jobs, including the 93 writing cases. Final benchmark setup truncates append-only fixtures after the initial copy and verifies bytes after warmup/every measured sample to avoid untimed reset work dominating CI wall time.

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

## Constructor PR streaming allocation guard

- Push run 43655910136 failed the existing header-based async streaming guard on Windows for net10.0 and net11.0: 6,592 and 6,664 excess bytes against the unchanged ±4,096-byte bound. The pull-request run 43655927539 at the identical commit passed all six platform/framework jobs. Constructor-specific tests passed in both runs.
- Treat this as intermittent allocation-measurement overhead; the exact allocation source has not been traced. The old 100,000-row warmup does not exercise the full measured workload through a sustained runtime warmup period.
- Warm both measured sizes (10,000 and 1,000,000 rows), plus the model-only baseline, for at least three seconds before collecting the existing minimum-of-three samples. Keep all six sync/async/header variants and the allocation bound unchanged. This follows the sustained/full-workload warmup used by performance harnesses.
- A temporary extra object per async row fails all three async variants at 23,760,000 excess bytes; sync variants pass. Remove the mutation before final validation. No production code or CI gate changes are required.
