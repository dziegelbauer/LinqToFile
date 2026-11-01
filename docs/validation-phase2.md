# Phase 2 validation

This historical record describes phase 2 at commit `cf12b87`.
The [phase 3 record](validation-phase3.md) describes the current mapping and conversion checks.

## Scope

Phase 1 is commit `9f8d8f5`.
Phase 2 adds CSV configuration and reader checks.
The local package version is `0.1.0-alpha.2`.

The public source still accepts file paths only.
The internal byte reader also works with non-seekable streams.
Public stream sources and async APIs remain planned for phase 4.

## Environment

The checks ran on Apple Silicon macOS.
The SDK version was 10.0.403.
The runtime version was 10.0.14.
The Native AOT target was `osx-arm64`.

## Results

| Check | Result |
|---|---|
| Release build with warnings as errors | Passed with zero warnings and zero errors |
| Unit tests | 220 passed |
| Original and configured samples under JIT | Passed |
| Samples under Native AOT | Passed |
| Local alpha.2 package under Native AOT | Passed |
| Runtime package dependencies | None |
| README and CSV-guide example checks | Passed |
| Git whitespace check | Passed |

Both sample modes printed these results:

```text
Adults: 1; first: Ada
Configured CSV: Ada;'Lovelace'
```

## Boundary tests

The byte-read tests use streams without position, length, or seek operations.
The test matrix covers five encodings with and without a BOM.
It uses decoded buffers of 1, 2, 3, and 7 characters.
It combines those buffers with byte reads limited to 1, 2, or 5 bytes.

The cases include escaped quotes, embedded CRLF, Unicode text, and supplementary characters.
Other cases cover short prefixes, invalid Unicode, and explicit decoder replacement.
The tests confirm BOM behavior with detection enabled and disabled.

The option tests cover independent header and field trimming.
They also cover duplicate headers after trimming, nullable empty values, and strict quote errors.
Length tests check exact limits, pre-trim content, escape syntax, embedded newlines, and unmapped columns.
Location checks confirm record starts, failure lines, and missing or extra column indexes.

## Allocation checks

The typed scan of 100,000 numeric records allocated 2,418,944 bytes.
That is 24.19 bytes per record, including one model object per record.
The median elapsed time was 16.18 ms in this local run.

A separate parser test reads 100,000 small records after warmup.
It permits at most 512 additional allocated bytes for the entire scan.
That test passes without model objects or field strings.
The [performance guide](performance.md) gives the measurement limits.

## Reproduce the checks

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release
dotnet run --project samples/NativeAot -c Release
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/phase2-aot
./artifacts/phase2-aot/NativeAot
dotnet run --project benchmarks/LinqToFile.Benchmarks -c Release
dotnet pack LinqToFile -c Release -o artifacts/feed
```

For the local package check, use a fresh cache after each package rebuild:

```sh
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/phase2-package-cache" --source "$PWD/artifacts/feed" --source https://api.nuget.org/v3/index.json -o artifacts/phase2-package-aot
./artifacts/phase2-package-aot/NativeAot
```

Windows and Linux execution checks remain part of the later platform-validation phase.
