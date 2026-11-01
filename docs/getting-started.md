# Get started

## Requirements

The .NET 10 SDK is necessary for this repository.
A native compiler for the target platform is also necessary for a Native AOT build.

## Build and test

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release
dotnet test tests/LinqToFile.Tests -c Release
dotnet run --project samples/NativeAot -c Release
```

## Install a local package

The alpha package contains the runtime and source generator.
It is a local test package. The project does not publish it to a public feed.

1. Make the package:

```sh
dotnet pack LinqToFile -c Release -o artifacts/feed
```

2. Restore the sample with the package:

```sh
dotnet restore samples/NativeAot -p:UseLocalPackage=true --configfile .github/package-validation.nuget.config
```

3. Run the sample with the package:

```sh
dotnet run --project samples/NativeAot -c Release -p:UseLocalPackage=true --no-restore
```

For another project, add a package reference to `LinqToFile` version `0.1.0-alpha.9`.
Use the local feed during restore.
A project that declares models must reference the package.
The caller does not register mappers.
The [release guide](release.md) describes the clean package-consumer check.

## Read records

A model must have the `[TabularRecord]` attribute and a `partial` declaration.
It must also have a public parameterless constructor.
A class without an explicit constructor has an implicit parameterless constructor.

`Csv<T>(path)` makes a source without an immediate file read.
Enumeration opens the file.
The [conversion guide](conversions.md) gives the scalar types and culture rules.
`Where`, `Select`, and `Take` can read records one at a time.
`ToList` and `ToArray` keep all selected records.
`OrderBy` must keep records before it can return a sorted result.

The library closes the file after completion, failure, or early exit from a `foreach` loop.
A caller that uses an enumerator directly must dispose that enumerator.

## Run Native AOT

On Apple Silicon macOS, run these commands:

```sh
dotnet publish samples/NativeAot -c Release -r osx-arm64 -o artifacts/aot
./artifacts/aot/NativeAot
```

For another platform, use its runtime identifier and executable name.
The sample uses a model from a separate assembly.
The generated static interface supplies its mapper without runtime discovery.

## Stream and async queries

The reader accepts supplied streams, synchronous stream factories, and async factories.
It supports async enumeration, cancellation, lists, arrays, and dictionaries.
The [source guide](sources-and-async.md) gives the lifetime rules and query examples.

## Read an XLSX workbook

The [XLSX reader](xlsx.md) supports sync/async reading with shared strings, cached formulas, native temporal conversions, and resource limits.
Check that guide before using an existing workbook.
The executable sample checks both Transitional and Strict XLSX workbooks.
