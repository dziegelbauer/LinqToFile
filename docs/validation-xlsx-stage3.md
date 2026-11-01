# XLSX stage 3 validation

## Status

Stage 3 implementation passes local validation.
Hosted Windows/Linux/macOS results are pending for these changes.
No public package release has been created.

The local artifact is `0.1.0-alpha.9`.
Validation uses macOS arm64, SDK 10.0.403, and runtime 10.0.14.

## Behavior checks

- Sync/async queries over paths, supplied streams, and fresh stream factories.
- Async-only factories reject sync enumeration without invoking the factory.
- Supplied-stream origin, single-use claims, immutable option clones, and leave-open rules.
- List, array, dictionary, selected dictionary values, comparers, and BCL async LINQ.
- Transitional/Strict shared strings, rich text, cached formulas, temporal values, and disk spill.
- Non-seekable copying with bounded buffers and shared workbook/string/index storage limits.
- Cancellation during copying, shared-string preprocessing, merge preflight, and reopened worksheet reads.
- Cleanup on completion, failure, cancellation, and early exit.
- Error types, cell locations, and raw-value privacy.
- Native numeric mapper allocation bounds and existing CSV regressions.

## Async I/O boundary

The reader uses .NET 10 `ZipArchive.CreateAsync` and entry `OpenAsync`.
XML and decompressed part reads use async APIs.
Copying and spill-file reads, writes, flushes, and owned disposal use async APIs.
Cancellation is checked between buffered XML nodes and text chunks as well as during I/O.

The BCL `Entries` property loads the central directory synchronously in read mode.
Seeks, file creation, metadata properties, and XML-reader disposal also remain synchronous.
The seekable test permits sync reads only in ZIP directory metadata and rejects sync part reads.
One sync metadata operation can finish before cancellation is observed.
There is no `Task.Run` wrapper or blocking wait on an async operation.
See the [.NET 10 source](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipArchive.cs).

## Performance review

The [performance guide](performance.md) reports two independent 50,000-record processes and a 200,000-record scan.
The [raw reports](measurements/README.md) include checksums, allocations, timing, heap samples, and temporary lengths.
Workloads cover native numbers, inline/repeated/unique strings, a small shared-string budget, large cells, non-seekable input, and first-record startup.
The measurements separate returned-value allocations from retained storage and startup costs.
They support bounded retained string/index storage after spill for these workloads.
They do not establish a total-memory cap or throughput guarantee.

The initial short warmup showed JIT-related ordering effects.
The final harness warms shared numeric/XML code for three seconds, then warms each case for at least five scans and one second.
The checked-in reports use this final method.
The 8 MiB default budget remains unchanged.

## Integration checks

The project and package samples include both workbook variants, async queries, factories, non-seekable copying, and disk spill.
The external consumer compiles XLSX models in a separate assembly with the packaged generator.
It runs XLSX and the packaged CSV README examples under JIT and Native AOT.
CI runs these checks on Windows x64, Linux x64, and macOS arm64.
It also uploads the XLSX benchmark report without a host-specific time gate.

All 719 tests pass. The Release solution build has zero warnings and errors.
Project-reference and alpha.9 package samples pass under JIT and Native AOT on macOS arm64.
The clean external package consumer passes under both JIT and Native AOT.
The documentation example checks pass, including the async XLSX guide example.

## Reproduction

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --nologo
dotnet run --project samples/NativeAot -c Release --no-build --no-restore
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/xlsx-stage3-aot
./artifacts/xlsx-stage3-aot/NativeAot
dotnet pack LinqToFile -c Release --no-build --no-restore -o artifacts/feed
dotnet restore samples/NativeAot -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage3-package-cache" --configfile .github/package-validation.nuget.config
dotnet run --project samples/NativeAot -c Release --no-restore -p:UseLocalPackage=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage3-package-cache"
dotnet publish samples/NativeAot -c Release -r osx-arm64 --no-restore -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage3-package-cache" -o artifacts/xlsx-stage3-package-aot
./artifacts/xlsx-stage3-package-aot/NativeAot
pwsh -NoProfile -File eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64
```

The sample adds this output line:

```text
XLSX async: factories, non-seekable input, LINQ, materializers, disk spill
```
