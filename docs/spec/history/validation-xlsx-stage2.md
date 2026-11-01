# XLSX stage 2 validation

## Status

Cell and storage implementation passes local checks on macOS arm64.
The user confirmed that CI passed after commit `32ff4d2`; stage 2 is complete.
Hosted results are user-reported and were not independently retrieved.
Async queries, factories, and non-seekable input remain stage 3 work.
No public package release has been created.

## Coverage

The local package version is `0.1.0-alpha.8`.
The SDK is 10.0.403 and the runtime is 10.0.14.
The Release solution build has zero warnings and errors.
All 688 tests pass, including documentation example checks.

The checks cover these cases:

- Shared-string headers and values in memory and on disk, in both workbook variants.
- Rich text, ignored phonetic content, text escapes, chunk/run boundaries, and raw UTF-16 preservation.
- Formula caches, shared-formula followers, empty cached strings, missing caches, and error cells.
- Exact numeric parsing, native ISO dates, both serial-date systems, durations, offset rules, and tick boundaries.
- Workbook, decompression, resolved-cell, temporary-byte, and shared-memory budget behavior.
- Cleanup after completion, early exit, setup failure, conversion failure, and limit failure.
- Existing CSV and stage 1 regressions, including allocation bounds and source ownership.

Temporal tests also pass with `TZ=Pacific/Auckland` and `TZ=America/Los_Angeles`.
The extended sample checks all temporal target types, formulas, and disk spill in both workbook variants.
It runs under JIT and Native AOT with a model in another assembly.
Project-reference and local-package JIT and Native AOT samples pass on macOS arm64.
The clean external package consumer also passes JIT, Native AOT, packaged README, and package metadata checks.

## Independent fixtures

Three unmodified files come from the Open XML SDK test corpus.
Their application metadata identifies Microsoft Excel.
They check a shared-string header, a formula header, numeric data, and ignored conditional formatting.
These are limited compatibility cases, not evidence that every workbook feature is supported.
The [fixture record](../../../tests/LinqToFile.Tests/Fixtures/Xlsx/README.md) contains the pinned upstream commit, paths, checksums, and MIT license attribution.
The runtime does not depend on the Open XML SDK.

## Memory and resource review

Shared strings stay in memory only while their accounted text, list, and reference-array storage fit the budget.
The default budget is 8 MiB.
Accounting conservatively estimates aligned string storage and retained indexing storage.
This is not a measurement of process working set.
Fixed parser buffers, active cells/rows, ZIP/workbook metadata, merges, and returned results are separate.

After spill, string data uses raw UTF-16 code units and a disk index uses 12 bytes per entry.
No growing offset array remains in RAM.
The temporary encoding is internal to one process and preserves unpaired UTF-16 units.
Cached strings can be reused as immutable returned values; spilled strings become owned returned strings.
Native booleans mapped to strings reuse canonical values.

Decompressed bytes include consumed metadata, shared strings, preflight, and reopened worksheet reads.
Cell limits apply after decoding and before trimming, including shared-string items during preprocessing.
Temporary-byte limits count aggregate live logical file lengths.
Configured-limit failures do not silently truncate data or fall back to unbounded RAM.
Temporary files use generated names and are deleted through owned file-handle disposal.

Native numeric parsing uses spans and the requested type directly.
Exact temporal conversion uses integer/decimal arithmetic, including BigInteger where needed, rather than double.
Existing CSV parser, mapper, and streaming allocation bounds still pass.
Broader XLSX throughput and retained-memory profiling remain stage 3 work.

## Reproduction

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --nologo
dotnet run --project samples/NativeAot -c Release --no-build --no-restore
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/xlsx-stage2-aot
./artifacts/xlsx-stage2-aot/NativeAot
dotnet pack LinqToFile -c Release --no-build --no-restore -o artifacts/feed
dotnet restore samples/NativeAot -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage2-package-cache" --configfile .github/package-validation.nuget.config
dotnet run --project samples/NativeAot -c Release --no-restore -p:UseLocalPackage=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage2-package-cache"
dotnet publish samples/NativeAot -c Release -r osx-arm64 --no-restore -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage2-package-cache" -o artifacts/xlsx-stage2-package-aot
./artifacts/xlsx-stage2-package-aot/NativeAot
pwsh -NoProfile -File eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64
```

The extended sample prints this line after the previous sample results:

```text
XLSX cells: shared strings, formulas, temporal values, disk spill
```

See the [XLSX guide](../../xlsx.md) for API, conversion, and limit rules.
