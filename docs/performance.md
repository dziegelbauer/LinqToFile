# Performance measurements

## Scope

The benchmark reads 100,000 integer records.
It makes the input before measurement.
It runs five warmup scans and five measured scans.
It reports the median elapsed time and allocated bytes.

The measured scan includes file opening, header binding, parser buffers, and one owned object per record.
It excludes input creation and benchmark result storage.
The benchmark checks a checksum to confirm that it reads every record.

## Run the benchmark

From the repository root, run this command:

```sh
dotnet run --project benchmarks/LinqToFile.Benchmarks -c Release
```

## Allocation behavior

The local baseline used macOS on arm64, SDK 10.0.403, and runtime 10.0.14.
The Release builds reported these results:

| Measurement | Phase 1 | Phase 2 |
|---|---|---|
| Records | 100,000 | 100,000 |
| Median elapsed time | 15.83 ms | 16.18 ms |
| Allocated bytes | 2,410,568 | 2,418,944 |
| Allocated bytes per record | 24.11 | 24.19 |

These values describe one local run for each phase.
Other machines and later runs can give different results.
The phase 2 scan uses 8,376 additional bytes for its fixed enumeration setup.
It includes the decoded-character input buffer and BOM prefix replay.
The measurements do not establish a change in throughput.

The parser reuses its character and field-range arrays within each enumeration.
Integer conversion does not make a temporary field string.
Each returned class instance still uses one allocation.
String properties use owned strings.

The current benchmark is an initial baseline.
It does not measure parser-only costs, string-heavy models, or async sources.
Elapsed time includes file-system effects and JIT behavior.
It does not establish a throughput guarantee.

The parser test reads 100,000 small records from a generated text source after a warmup.
It permits at most 512 additional allocated bytes across that entire scan.
This check excludes model objects and owned strings.
It checks that parser storage does not grow with record count.

## Later validation

Later phases will compare parser-only and typed-read costs.
They will also measure large records, large files, and concurrent enumerations.
Buffer and pooling decisions will follow those measurements.
