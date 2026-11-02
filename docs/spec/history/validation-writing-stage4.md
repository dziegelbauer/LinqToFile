# CSV writing stage 4 validation

Date: 2026-11-02. Branch: feature/row-writing-plan.
Local checks are complete; hosted matrix verification is pending the feature-branch push.
Do not merge: the user requires human approval for every merge to main.

## Measurements and tuning

A BCL-only --writing benchmark mode separates input enumeration, generated formatting/escaping, old-file validation,
insertion-offset scanning, byte copying, suffix rotation, and complete creation/append/quick-append/insertion.
The full matrix covers numeric, quoted Unicode/multiline text, and 4096-character fields, synchronous/asynchronous
input and I/O, both write modes, and 100 single-row calls versus one range. New input reuses one model to isolate
library costs. Resetting fixtures and exact output-byte verification are outside timed/allocation intervals.

Every case warms for at least five scans and one accumulated second of measured work, then records five samples.
Numeric formatting receives an initial three-second warmup. Process-wide precise allocation counters include
async continuations. Run one benchmark process at a time, outside a debugger. Timing is informational, not a CI gate.

The initial byte-position reader performed a Rune decode and virtual GetChars call for each ASCII byte.
A measured UTF-8 ASCII path now copies characters and original offsets directly; valid non-ASCII runes decode once.
Invalid input still uses the configured fallback and original-byte unit boundaries. Cancellation remains checked
within long ASCII runs. The full correctness suite, including malformed encodings and offset/fault tests, passes.

Local median comparisons (milliseconds; same 10,000 new/100,000 old numeric configuration):

| Work | Baseline | Optimized | Ratio |
|---|---:|---:|---:|
| Numeric byte-offset scan | 1.618 | 0.938 | 1.73x |
| Quoted Unicode byte-offset scan | 7.838 | 4.565 | 1.72x |
| Large-field byte-offset scan | 30.240 | 15.554 | 1.94x |
| Numeric direct insertion | 2.138 | 1.467 | 1.46x |
| Quoted direct insertion | 8.854 | 5.596 | 1.58x |
| Large-field direct insertion | 32.006 | 17.978 | 1.78x |

Quoted input caps at 10,000 new/50,000 old rows; large fields cap at 200 new/1,000 old rows.
These are measurements of this host and corpus, not portable throughput guarantees.

## Evidence and tradeoffs

Raw independent process reports:
- [Baseline before tuning](measurements/writing-stage4-baseline-macos-arm64.json): 81 cases.
- [Optimized same-size run](measurements/writing-stage4-optimized-macos-arm64.json): 81 cases.
- [Scale run](measurements/writing-stage4-scale-macos-arm64.json): 81 cases, 50,000 new and 1,000,000 old numeric rows.
- [Single calls versus ranges](measurements/writing-stage4-batches-macos-arm64.json): 12 cases.
- [One-character read buffers](measurements/writing-stage4-tiny-macos-arm64.json): 6 scan cases.

The final harness adds the 12 short-batch comparisons to its default matrix (93 cases).
It also reports expected peak staged-result bytes separately from observed temporary-file snapshots.
The preserved earlier full reports predate those extra metadata fields; do not mislabel snapshots as full-result peaks.

Observed numeric formatting: zero allocated bytes for the reused model.
Synchronous numeric insertion stays at 129,112 B/operation in staged mode and 193,384 B/operation in direct mode
in both size runs, despite 10x old rows and 5x new rows. The numeric byte-offset scan stays at 49,800 B/operation.
Existing tests bound new-row allocation growth; a new 10,000-versus-1,000,000 old-row test bounds scan allocation
growth to +/-4096 bytes. Caller-owned models/strings are deliberately excluded by reuse.

Async allocations grow with I/O refills/continuations: numeric direct insertion rose from 215,648 to 392,536 B
over the scale change. This is total allocation, not retained memory. Do not claim allocation-free async I/O.
Heap probes sample active operations at 1, 10%, and all supplied rows. They show bounded active storage and zero
temporary-file bytes in direct mode. GC snapshots include ambient runtime/root release; negative baseline deltas
are possible and are not evidence of negative allocation. Final input callbacks precede suffix copy/commit, so
observed staged-file sizes can be below the eventual complete result. Total staged-result size is predictable
from existing bytes plus added bytes (or header plus data for creation).

For 100 numeric quick direct append calls, the sync median was 3.736 ms / 2,867,200 B; one range took
0.114 ms / 28,680 B. For large fields, 100 calls took 8.041 ms / 6,053,824 B; one range took
0.821 ms / 60,544 B. The user guide therefore recommends ranges to amortize preparation.
Default validated mutations retain full scan costs; quick staged append still pays the byte copy.
Direct insertion avoids temporary disk space but still moves suffix/new bytes repeatedly.
Keep existing buffer defaults and the 64 KiB movement blocks: no additional tuning or pooling is justified by this run.

One accidental benchmark invocation used an unsupported --only=value spelling and was stopped.
Its partial output is not included in evidence. The harness now validates options before creating fixtures;
only the subsequent completed batch report is preserved. The owned abandoned temporary directory was removed.

## User introductions and runnable documentation

The root README now introduces import/report/export use cases, benefits, supported read/write capabilities,
installation, one annotated model, CSV queries, async dictionaries, CSV writes, XLSX selection, and detailed links.
All links use absolute repository URLs so the same introduction works on NuGet.

The package now ships the root README directly. The duplicate docs/package-readme.md is removed.
Package verification asserts exact README equality and compiles/executes all five packaged C# blocks in the clean
external consumer. QuickStartSample executes the same API examples in project and package Native AOT samples.
Documentation drift tests check these snippets. The performance guide explains practical scan/copy/movement,
memory, range batching, and write-mode costs without implementation history. Future features remain on the roadmap.

## Local release checks

- Warning-free Release solution build and all 1,025 tests pass.
- Project JIT sample, including every quick-start and write API example, passes.
- Local package base version 0.1.0-alpha.13; installation/current artifact docs match.
- Clean external package consumer (fresh cache, separate model/application assemblies) passes JIT and Native AOT:
  exact main/NuGet README examples, CSV/XLSX reads, all write modes/ranges, scalar mappings, package metadata,
  Apache-2.0 license, two-DLL payload, and zero runtime package dependencies.
- Project-reference and UseLocalPackage=true Native AOT checks are run before the feature-branch push.
- Workflow actionlint and git diff --check pass.
- Runtime source review: BCL-only dependencies and no reflection, task offloading, or synchronous waits added.

## Hosted acceptance

CI now records the 93-case writing report and retains it with each platform's validation artifacts.
All Windows/Linux/macOS jobs still gate main-only NuGet publication; feature branches and pull requests validate only.
The feature branch will be committed/pushed for matrix execution, then a pull request opened for human review.
Record actual run URLs/results after completion. Do not claim hosted success before it occurs or merge the PR.

Keep the pre-existing docs/spec/release.md publication-verification note outside the writing commit.
