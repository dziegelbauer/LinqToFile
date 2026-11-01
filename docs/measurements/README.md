# Phase 5 measurement files

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
