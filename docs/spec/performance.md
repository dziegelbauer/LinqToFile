# Performance and CI decisions

## Confirmed
- GitHub is the intended hosting service. Use GitHub Actions for CI.
- Runtime remains BCL-only. Benchmark and diagnostic tools are development-only.
- Performance tuning must follow evidence; buffer/pooling choices need no further user decision initially.

## Phase 5 implementation
- BCL harness separates parser-only, generated numeric mapping, sync/async numeric files, owned strings with quoted Unicode/multiline input, 4096-character fields, and async list/dictionary materialization.
- Minimum five warmup scans and one second per workload; raw samples and environment in JSON; process-wide allocation counters cover async continuations. Run processes sequentially without a debugger.
- Fixed-storage generated streams compare 10,000 with 1,000,000 records, both sync and async. Additional allocation must match plain model allocations within 4096 bytes. Existing parser and mapper allocation bounds remain.
- Keep 4096-character default and current per-enumeration arrays: current buffer trials show workload tradeoffs, not a consistent gain. No pooling or runtime parser changes in this slice.
- CI: Windows x64, Linux x64, macOS arm64; warning-free Release build, tests, JIT sample, project and package Native AOT publish/run, recorded benchmarks. Timing is informational, with no host-specific time gate.
- Hosted runs are pending repository publication. Do not mark phase 5 complete before the platform/AOT matrix passes. Other .NET platforms remain a compatibility requirement, not a claim of execution on all targets.
