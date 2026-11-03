# Get started

## Requirements

Use .NET 10 or later.
The runtime uses only the BCL.
The package includes the source generator that creates typed mappers during compilation.

A native compiler for your target platform is necessary only when you publish with Native AOT.

## Install the package

To install a published prototype prerelease from nuget.org, run:

```sh
dotnet add MyApp/MyApp.csproj package LinqToFile --prerelease --source https://api.nuget.org/v3/index.json
```

Replace the project path with your own path.
Add the package to each project that declares mapped models.
Use an explicit `--version` when you need a reproducible package version.

## Build a local package

The repository provides a local prerelease package.
To build it, run this command from the repository root:

```sh
dotnet pack LinqToFile -c Release -o artifacts/feed
```

Add the package to each project that declares mapped models.
For an application project, use this command with your local feed path:

```sh
dotnet add MyApp/MyApp.csproj package LinqToFile --version 0.1.0-alpha.15 --source /absolute/path/to/LinqToFile/artifacts/feed
```

Replace the project path and feed path with your own paths.
Keep the local feed available when you restore the package on another machine.

## Declare a model

Use the model and query in the [repository README](../README.md).
A mapped class must be public, partial, and non-abstract.
It must have a public parameterless constructor.
A class without an explicit constructor has an implicit parameterless constructor.

Mark the class with `[TabularRecord]`.
Properties need a public setter or init accessor.
Use `[Column("header")]` when a column name differs from the property name.
Use `[Ignore]` to exclude a property.
The [mapping reference](reference.md) gives the complete model rules.

## Read records

Use `TabularFile.Csv<T>(path)` for CSV or `TabularFile.Xlsx<T>(path)` for XLSX.
Both formats require column headers.
Creating a source does not open the file.
Enumeration opens the source, validates headers, and creates typed records.

Use standard LINQ to select records.
`Where`, `Select`, and `Take` can process them one at a time.
`ToList` and `ToArray` keep the selected records in memory.
Configure the source before you apply LINQ operators.

The library closes owned files after completion, failure, or early exit from a loop.
Dispose an enumerator that you use directly.
See [sources and async queries](sources-and-async.md) for supplied streams, factories, cancellation, and ownership.

## Select a format

- Use the [CSV guide](csv-and-errors.md) for encoding, delimiters, quotes, trimming, and size limits.
- Use the [XLSX guide](xlsx.md) for worksheets, cached formulas, native dates, and temporary storage.
- Use the [conversion guide](conversions.md) for property types and culture rules.

## Use Native AOT

Mappings work with Native AOT without runtime registration or reflection.
Each project that declares models must reference the package so its generator can run.

Publish your application with its target runtime identifier:

```sh
dotnet publish MyApp/MyApp.csproj -c Release -r osx-arm64 -p:PublishAot=true
```

Use the runtime identifier for your target platform.
For example, use `win-x64` for Windows x64 or `linux-x64` for Linux x64.

## Run the repository sample

From the repository root, run:

```sh
dotnet run --project samples/NativeAot -c Release
```

The sample runs CSV/XLSX queries and CSV creation, append, and insertion with models from a separate assembly.
