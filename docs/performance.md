# Performance measurements

## Scope

The phase 1 benchmark reads 100,000 integer records.
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
The Release build reported these results:

| Measurement | Local result |
|---|---|
| Records | 100,000 |
| Median elapsed time | 15.83 ms |
| Allocated bytes | 2,410,568 |
| Allocated bytes per record | 24.11 |

These values describe one local run.
Other machines and later runs can give different results.

The parser reuses its character and field-range arrays within each enumeration.
Integer conversion does not make a temporary field string.
Each returned class instance still uses one allocation.
String properties use owned strings.

The current benchmark is an initial baseline.
It does not measure parser-only costs, string-heavy models, or async sources.
Elapsed time includes file-system effects and JIT behavior.
It does not establish a throughput guarantee.

## Later validation

Later phases will compare parser-only and typed-read costs.
They will also measure large records, large files, and concurrent enumerations.
Buffer and pooling decisions will follow those measurements.
