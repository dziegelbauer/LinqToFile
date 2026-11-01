# Phase 3 validation

## Scope

Phase 2 is commit `cf12b87`.
Phase 3 completes scalar conversions, culture options, and mapping diagnostics.
The local package version is `0.1.0-alpha.3`.

The public source still accepts file paths only.
Public stream inputs and async queries remain planned for phase 4.

## Environment

The checks ran on Apple Silicon macOS.
The SDK version was 10.0.403.
The runtime version was 10.0.14.
The Native AOT target was `osx-arm64`.

## Results

| Check | Result |
|---|---|
| Release build with warnings as errors | Zero warnings and zero errors |
| Unit tests | 323 passed |
| Conversion tests under Pacific/Auckland | 66 passed |
| Conversion tests under America/Los_Angeles | 66 passed |
| Separate model assembly under JIT and Native AOT | Passed |
| Local alpha.3 package under JIT and Native AOT | Passed |
| Runtime package dependencies | None |
| Runnable documentation examples | Passed |

The sample printed these results:

```text
Adults: 1; first: Ada
Configured CSV: Ada;'Lovelace'
Scalar mapping: Ready; offset: -07:00:00
```

It also checked French decimal parsing without additional output.

## Conversion checks

Tests cover every supported scalar type and its nullable value form.
Numeric checks include range boundaries, overflow, finite values, signs, whitespace, and exponent rules.
The culture checks include decimal commas, localized dates, and a caller culture that changes after configuration.

Temporal tests check offset-free DateTime values and explicit-offset DateTimeOffset values.
They include minimum and maximum date-range edges.
Separate test processes use Auckland and Los Angeles time zones.

Enum checks include aliases, case sensitivity, numeric-token rejection, and unnamed flag combinations.
They also cover signed flags, unsigned high bits, obsolete names, and all eight enum underlying types.
The generated code uses constant values without runtime enum discovery.

## Mapping checks

Tests cover inherited required properties, override annotations, and hidden-member conflicts.
They also cover generic base substitution and nested partial containers.
Missing headers identify the target property.
Parser and record-width errors identify it when the header supplies the mapping.
Comparison-dependent property collisions throw during configuration.

The generator reports unsupported models, members, and conversion types.
Numeric `nint` and `nuint` remain deferred.
Raw field values and inner parser exception text remain absent from conversion errors.

## Allocation checks

The numeric scan of 100,000 records allocated 2,418,992 bytes.
That is 24.19 bytes per record, including one owned model object per record.
The median elapsed time was 17.50 ms in this local run.

A separate test compares 10,000 numeric/enum mappings with 10,000 plain model allocations.
After warmup, the extra allocation stays within 512 bytes for the entire loop.
The parser's 100,000-record storage check also remains within its allocation bound.

The [performance guide](performance.md) gives the measurement limits.
These local results do not establish a throughput guarantee.

## Reproduce the checks

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release
env TZ=Pacific/Auckland dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~ConversionTests
env TZ=America/Los_Angeles dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~ConversionTests
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/phase3-aot
./artifacts/phase3-aot/NativeAot
dotnet run --project benchmarks/LinqToFile.Benchmarks -c Release
dotnet pack LinqToFile -c Release -o artifacts/feed
```

For a package check, use a fresh cache after each package rebuild:

```sh
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/phase3-final-cache" --source "$PWD/artifacts/feed" --source https://api.nuget.org/v3/index.json -o artifacts/phase3-final-aot
./artifacts/phase3-final-aot/NativeAot
```

The `env TZ` commands describe the Unix host checks.
Windows and Linux execution checks remain planned for the platform-validation phase.
