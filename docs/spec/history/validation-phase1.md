# Phase 1 validation

This historical record describes phase 1 at commit `9f8d8f5`.
The [phase 2 record](validation-phase2.md) describes the complete CSV reader checks.

## Environment

The checks ran on Apple Silicon macOS.
The SDK version was 10.0.403.
The runtime version was 10.0.14.
The Native AOT target was `osx-arm64`.

Windows and Linux execution checks belong to a later phase.
These local results do not replace those checks.

## Results

| Check | Result |
|---|---|
| Release solution build with warnings as errors | Passed with zero warnings and zero errors |
| xUnit tests | 43 passed |
| Separate model assembly under JIT | Passed |
| Native AOT publish and execution | Passed |
| Local package consumption under JIT | Passed |
| Local package consumption under Native AOT | Passed |
| Package license metadata | Apache-2.0 |
| Runtime package dependencies | None |
| README example comparison | Passed |

Both executable samples printed `Adults: 1; first: Ada`.
The examples use a model from a separate assembly.

The tests cover required and init properties, generic helpers, nested models, and generator diagnostics.
They also cover header rules, quotes, record width, retained strings, deferred reads, disposal, and encoding.
The encoding tests include strict UTF-8 with a BOM and detection of UTF-8, UTF-16, and UTF-32 byte orders.

## Commands

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release
dotnet run --project samples/NativeAot -c Release
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/aot
./artifacts/aot/NativeAot
dotnet pack LinqToFile -c Release -o artifacts/feed
```

For the package check, use a new package cache after each package rebuild.
NuGet can otherwise keep an earlier package with the same alpha version.

```sh
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/fresh-cache" --source "$PWD/artifacts/feed" --source https://api.nuget.org/v3/index.json -o artifacts/package-aot
./artifacts/package-aot/NativeAot
```

The local check used absolute paths for its package source and cache.
Package-mode samples use separate output and intermediate directories.
Thus, a package restore does not change the normal project-reference build.

## Allocation baseline

The benchmark scanned 100,000 integer records.
The median elapsed time was 15.83 ms.
The scan allocated 2,410,568 bytes, or 24.11 bytes per record.
The count includes one owned model object per record.
The [performance guide](performance.md) gives the measurement limits.

## Phase boundary

Phase 1 reads file paths through synchronous enumeration.
Its supported value types are string, int, decimal, bool, and Guid, with nullable value forms.
The source has encoding and header-comparison options.

Additional conversions, stream sources, async APIs, and full CSV configuration remain planned work.
The [phased plan](../plan.md) gives the next acceptance criteria.
