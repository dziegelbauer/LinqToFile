# Performance measurements

## Run the benchmark

Run the benchmark in Release mode without a debugger.
Run one benchmark process at a time.

```sh
dotnet run --project benchmarks/LinqToFile.Benchmarks -c Release -- --output artifacts/benchmarks.json
```

The default numeric workload contains 100,000 records.
Use these options to change the measurement:

| Option | Default | Meaning |
|---|---|---|
| --rows | 100000 | Numeric record count |
| --iterations | 7 | Measured scans per workload |
| --buffer | 4096 | Decoded-character input buffer size |
| --output | No file | JSON report path |

Numeric options must be positive integers.
Each workload runs at least five warmup scans and one second of warmup.
The report contains every measured scan, its checksum, and its allocation count.
It also contains runtime and operating-system information.
The displayed values are medians. An even scan count uses the upper middle value.

For a larger scan, use this command:

```sh
dotnet run --project benchmarks/LinqToFile.Benchmarks -c Release -- --rows 1000000 --iterations 5 --output artifacts/million.json
```

## Measurement scope

Input creation is outside measurement.
File scans include stream opening, decoding, header binding, parser buffers, and returned objects.
String models also include owned string allocations.
The file cache can affect elapsed time.

The parser-only scan uses a prepared string and a text reader.
It excludes file I/O, decoding, schema binding, and typed mapping.
The mapping-only scan repeatedly maps one prepared numeric row.
It includes conversion and one returned class instance per record.
These separate measurements do not add up exactly to the file scan.

Quoted input contains Unicode, escaped quotes, a delimiter, and an embedded newline.
This workload contains at most 20,000 records.
The large-field workload contains at most 1,000 records, each with 4096 characters.
Async list and dictionary scans include collection construction and checksum enumeration.

Allocation counts use `GC.GetTotalAllocatedBytes(precise: true)` across the process.
This counter includes async continuations on other threads.
It can also include runtime background work.
Do not compare it directly with the older per-thread measurements below.

## Phase 5 baseline

The local environment used macOS arm64, SDK 10.0.403, and runtime 10.0.14.
The runtime parser and query code had no performance changes after phase 4.
[Raw reports](measurements/README.md) contain the samples.

Two separate processes produced these default-buffer results:

| Workload | Records | Median time, run 1 / run 2 | Allocated bytes per record, run 1 / run 2 |
|---|---|---|---|
| Numeric file, sync | 100,000 | 2.15 / 2.33 ms | 24.19 / 24.19 |
| Numeric file, async | 100,000 | 6.48 / 5.77 ms | 24.82 / 24.82 |
| Numeric list, async | 100,000 | 5.41 / 5.43 ms | 45.80 / 45.80 |
| Numeric dictionary, async | 100,000 | 6.92 / 8.61 ms | 109.42 / 109.38 |
| Quoted strings, sync | 20,000 | 1.57 / 1.74 ms | 80.95 / 80.95 |
| Quoted strings, async | 20,000 | 2.23 / 2.26 ms | 83.32 / 83.33 |
| Large field, sync | 1,000 | 8.64 / 8.86 ms | 8271.63 / 8271.63 |
| Parser only | 100,000 | 1.23 / 1.25 ms | 0.11 / 0.11 |
| Mapping only | 100,000 | 0.61 / 0.59 ms | 24.00 / 24.00 |

The million-record numeric scan used 24.02 bytes per record synchronously.
The async scan used 24.73 bytes per record.
Async I/O can allocate at buffer refills even when mapping needs no temporary strings.
The list and dictionary workloads use more memory because they retain their records.

## Buffers and streaming memory

These single-process trials used 100,000 numeric records:

| Buffer, characters | Sync time | Async time | Parser-only time |
|---|---|---|---|
| 512 | 2.47 ms | 6.02 ms | 1.40 ms |
| 4096, run 1 | 2.15 ms | 6.48 ms | 1.23 ms |
| 4096, run 2 | 2.33 ms | 5.77 ms | 1.25 ms |
| 16384 | 2.45 ms | 4.41 ms | 1.35 ms |

The larger buffer helped async numeric input in this trial.
It also increased fixed setup allocation and did not improve every workload.
The default remains 4096 characters.
Use `WithBufferSize` to measure another size with your files.
These trials do not establish a general optimum.

The parser reuses its character and field-range arrays within each enumeration.
Pooling is not used.
Current results show no growing parser allocation cost for fixed-size records.
They do not establish a benefit from pooling those arrays.

The benchmark also samples the live managed heap during one numeric enumeration.
It forces a collection at increasing record counts and retains no output collection.
The million-row run reported these process-wide values:

| Records read | Live managed bytes |
|---|---|
| 10,000 | 14,115,384 |
| 100,000 | 14,115,488 |
| 1,000,000 | 14,115,552 |

These absolute values include benchmark state and the prepared parser input string.
They are not a measurement of query memory alone or native process memory.
The near-constant values support bounded storage during this scan.
Larger individual records can still grow parser storage.

Regression tests use a fixed-storage, non-seekable generated stream.
Both sync and async tests compare 10,000 records with 1,000,000 records.
The extra allocation must match plain model allocations within 4096 bytes.
All async reads in this test complete inline, so a per-thread counter is valid.
Separate tests bound parser-only and numeric/enum mapping allocation after warmup.

## Sampling profile

A local sample used the development-only `dotnet-trace` tool, version 10.0.750501.
It used the `dotnet-sampled-thread-time` profile over all benchmark workloads.
The trace includes warmups, setup, and thread waits.
Its percentages are not CPU utilization or individual workload timings.
Most exclusive samples were in wait functions.
Among active managed methods, `StreamReader.ReadSpan` and `CsvParser.ParseAvailable` were visible.
The profile did not establish a reason to change the shared format-reader boundary.

To repeat the sample, build the benchmark first. Then run:

```sh
dotnet tool install dotnet-trace --version 10.0.750501 --tool-path artifacts/tools --allow-roll-forward
./artifacts/tools/dotnet-trace collect --profile dotnet-sampled-thread-time --output artifacts/phase5.nettrace -- dotnet benchmarks/LinqToFile.Benchmarks/bin/Release/net10.0/LinqToFile.Benchmarks.dll --iterations 3
./artifacts/tools/dotnet-trace report artifacts/phase5.nettrace topN -n 15
```

See the [Microsoft diagnostic tool reference](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) for tool options.
Trace files stay in the ignored artifacts directory.

## Earlier measurements

The older harness used five scans for warmup and five measured scans.
It measured only the numeric file scan with a per-thread allocation counter.

| Measurement | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|---|---|---|---|---|
| Records | 100,000 | 100,000 | 100,000 | 100,000 |
| Median time | 15.83 ms | 16.18 ms | 17.50 ms | 20.30 ms |
| Allocated bytes | 2,410,568 | 2,418,944 | 2,418,992 | 2,419,072 |

The short warmup did not produce stable timing results.
The phase 5 warmup gave much lower times without a runtime code change.
Do not use these tables to claim a performance improvement or regression between phases.
The measurements do not establish a throughput guarantee.
