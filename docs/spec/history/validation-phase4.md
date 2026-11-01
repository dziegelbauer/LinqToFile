# Phase 4 validation

## Scope

Phase 3 is commit `2560217`.
Phase 4 adds stream sources, async enumeration, cancellation, and async materialization.
It also separates shared runtime layers from the CSV adapter.
The local package version is `0.1.0-alpha.4`.

## Environment

The checks ran on Apple Silicon macOS.
The SDK version was 10.0.403.
The runtime version was 10.0.14.
The Native AOT target was `osx-arm64`.

## Results

| Check | Result |
|---|---|
| Release build with warnings as errors | Zero warnings and zero errors |
| Unit tests | 483 passed |
| Original and async samples under JIT | Passed |
| Samples under Native AOT | Passed |
| Local alpha.4 package under Native AOT | Passed |
| Runnable documentation examples | Passed |
| Synthetic non-CSV reader through shared queries | Passed |

The sample printed these results:

```text
Adults: 1; first: Ada
Configured CSV: Ada;'Lovelace'
Scalar mapping: Ready; offset: -07:00:00
Async CSV: 1 adult; 2 dictionary entries
```

## Async parsing checks

The existing synchronous reader and conversion tests still pass.
An additional 120-case matrix compares async and sync parsing.
It combines five encodings, optional BOMs, tiny character buffers, and limited byte reads.

The async test streams reject synchronous reads.
The cases include escaped quotes, embedded CRLF, supplementary characters, and short reads.
Error-parity tests compare exception types, record numbers, start lines, failure lines, and columns.

Sync and async CSV reads use one parsing state machine.
Only buffer refills differ.

## Lifetime and cancellation checks

Tests cover these conditions:

- Supplied streams with both leave-open modes.
- Unused enumerators that do not claim the stream.
- One atomic claim across derived configurations and both enumeration modes.
- A race between synchronous and asynchronous enumeration.
- Independent concurrent file and factory enumerations.
- Streams that start at a nonzero position without seeking.
- Async-only factories that reject synchronous enumeration without a factory call.
- Factory results that are null or unreadable.
- Completion, early exit, parser failure, conversion failure, I/O failure, and selector failure.
- Cancellation before the first move, during pending I/O, and between buffered records.
- Cancellation during factory opening after a stream becomes available.
- List, array, and dictionary materialization.

Owned streams use async disposal during async enumeration.
Caller-owned streams remain open.
Duplicate-key errors keep the ArgumentException type without the raw key.

## Format independence

The runtime remains one project and one consumer package.
Sources, decoding, schema binding, queries, and mapping have shared folders.
CSV parsing and options have a separate `Formats/Csv` folder.

Generated mappers now consume `TabularRow`.
A test-only byte format supplies that row through the internal reader contract.
It reuses the same source ownership, schema binding, and typed query engine in both modes.
It does not use the CSV parser.

The row currently holds text-backed cells.
A public provider API remains deferred.

## Allocation baseline

The synchronous scan of 100,000 numeric records allocated 2,419,072 bytes.
That is 24.19 bytes per record, including one owned model object per record.
The scan adds 80 bytes of fixed setup compared with phase 3.

The median local time was 20.30 ms.
This value is higher than the earlier local samples.
Phase 5 will profile the shared reader boundary and run broader repeated measurements.
The [performance guide](performance.md) gives the measurement limits.

## Reproduce the checks

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release
dotnet run --project samples/NativeAot -c Release
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/phase4-aot
./artifacts/phase4-aot/NativeAot
dotnet pack LinqToFile -c Release -o artifacts/feed
```

For a package check, use a fresh cache after each package rebuild:

```sh
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/phase4-package-cache" --source "$PWD/artifacts/feed" --source https://api.nuget.org/v3/index.json -o artifacts/phase4-package-aot
./artifacts/phase4-package-aot/NativeAot
```

Windows and Linux execution checks remain planned for phase 5.
