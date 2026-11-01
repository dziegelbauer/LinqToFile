# XLSX stage 1 validation

## Status

The integrated XLSX foundation passes local checks on macOS arm64.
Full XLSX support remains incomplete. Stages 2 and 3 are not implemented.
The existing CI workflow will run the new tests and samples on Windows, Linux, and macOS after publication.
Those hosted results are pending.

## Local evidence

The local SDK is 10.0.403. The runtime is 10.0.14.
The Release solution builds with zero warnings and errors.
All 583 tests pass, including the previous 485 checks, 97 XLSX cases, and one new documentation-example check.

The tests cover these cases:

- Transitional and Strict package relationships and namespaces.
- Named/default/hidden worksheet selection and relocated workbook parts.
- Header selection, sparse coordinates, empty rows, trailing formatting, and schema failures.
- Exact native integer parsing, Int128/UInt128 boundaries, decimal precision, boolean/type boundaries, and text culture.
- Merge rejection before returning records, including merges in trailing rows.
- Structured cell/worksheet diagnostics without raw values or parser inner exceptions.
- Malformed ZIP/XML, missing/duplicate parts, unsupported content types, DTDs, external relationships, and required markup.
- Deferred path access, repeated paths, supplied-stream claiming, nonzero stream origins, ownership, and early exit.
- Explicit rejection of stage 2 cell features.

The cross-assembly sample reads both XLSX variants under JIT and Native AOT.
It also passes through the alpha.7 package with a fresh package cache.
The package check confirms the runtime and generator DLLs, Apache-2.0 license, and empty runtime dependency group.
The external clean-consumer check still passes its CSV/README tests under JIT and Native AOT.
The repository sample supplies the XLSX package-consumer check in this stage.

## Allocation and architecture review

The worksheet is streamed into reusable character and field-range buffers.
Native number conversion uses character spans and the requested numeric type directly.
It does not convert every number through double or create a field string for numeric parsing.
Returned models and strings own their values.
XML coordinate/type attributes and package/workbook metadata can still allocate.
Existing CSV parser, mapper, and million-row streaming allocation tests continue to pass.

ZIP directory and workbook metadata are retained per enumeration.
Merge declarations are retained during preflight.
Buffers can grow with the largest active row.
The preflight reads the worksheet before streaming starts and does not convert future data rows.
These checks do not establish a throughput guarantee or a fixed total memory bound.
Broader XLSX performance measurements remain stage 3 work.

## Fixture provenance

Fixtures are generated from XML and ZIP literals with BCL types.
They do not use a third-party spreadsheet library.
They provide controlled structural and conversion cases rather than independent Excel/LibreOffice compatibility evidence.
Independent producer fixtures remain planned for stage 2.

Primary references used during implementation include the [SpreadsheetML structure guide](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/structure-of-a-spreadsheetml-document) and the [BCL XML reader settings](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings?view=net-10.0).
The runtime does not depend on the Open XML SDK.

## Reproduction

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --nologo
dotnet run --project samples/NativeAot -c Release --no-build --no-restore
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/xlsx-stage1-aot
./artifacts/xlsx-stage1-aot/NativeAot
dotnet pack LinqToFile -c Release --no-build --no-restore -o artifacts/feed
dotnet restore samples/NativeAot -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage1-package-cache" --configfile .github/package-validation.nuget.config
dotnet run --project samples/NativeAot -c Release --no-restore -p:UseLocalPackage=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage1-package-cache"
dotnet publish samples/NativeAot -c Release -r osx-arm64 --no-restore -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/xlsx-stage1-package-cache" -o artifacts/xlsx-stage1-package-aot
./artifacts/xlsx-stage1-package-aot/NativeAot
pwsh -NoProfile -File eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64
```

The XLSX sample prints this result after the existing CSV results:

```text
XLSX: Transitional and Strict; typed records: Ada
```

See the [XLSX guide](xlsx.md) for the current API and limitations.
