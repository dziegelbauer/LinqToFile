# Measurement files

## CSV phase 5

These files contain the raw benchmark samples from macOS arm64.
The SDK was 10.0.403. The runtime was 10.0.14.
The runtime library had no performance changes after phase 4.

Each process used at least five warmup scans and one second of warmup per workload.
The default buffer contains 4096 decoded characters.

| File | Records in numeric workloads | Buffer | Measured scans |
|---|---|---|---|
| phase5-default-1.json | 100,000 | 4096 | 7 |
| phase5-default-2.json | 100,000 | 4096 | 7 |
| phase5-million.json | 1,000,000 | 4096 | 5 |
| phase5-buffer512.json | 100,000 | 512 | 7 |
| phase5-buffer16384.json | 100,000 | 16384 | 7 |

See [performance measurements](../performance.md) for commands, scope, and limits.

## XLSX stage 3

The XLSX reports use the same SDK, runtime, and platform.
Each process first warms shared numeric/XML code for at least three seconds.
Each workload then runs at least five warmup scans and one second of warmup.
Input generation is outside measurement. Each scan includes archive/schema setup and merge preflight.

| File | Records | Measured scans |
|---|---|---|
| [xlsx-stage3-run1.json](xlsx-stage3-run1.json) | 50,000 | 5 |
| [xlsx-stage3-run2.json](xlsx-stage3-run2.json) | 50,000 | 5 |
| [xlsx-stage3-200000.json](xlsx-stage3-200000.json) | 200,000 | 3 |

Large-cell cases use 500 records with 16,384 UTF-16 units each.
First-record cases return one record after full preprocessing.
Heap probes retain the enumerator and its current record, with no result collection.
Heap deltas include process-wide effects and collection noise; they are not a strict query-memory measurement.
