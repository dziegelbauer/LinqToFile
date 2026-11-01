# LinqToFile

LinqToFile reads tabular files as typed C# records. Standard LINQ operators filter and select the records.
CSV is complete for the agreed initial scope. XLSX stage 2 supports synchronous reading with storage controls.
The runtime uses only the .NET Base Class Library (BCL). The minimum target is .NET 10.

This repository contains the reader, typed mapper, and sync/async query API.
The GitHub Actions workflow validates Windows, Linux, and macOS.
The API can change before a stable release.
The package includes a source generator. The generator makes the mapper during compilation.
The runtime does not use reflection.

## Run the sample

From the repository root, run this command:

```sh
dotnet run --project samples/NativeAot -c Release
```

The sample prints this result:

```text
Adults: 1; first: Ada
Configured CSV: Ada;'Lovelace'
Scalar mapping: Ready; offset: -07:00:00
Async CSV: 1 adult; 2 dictionary entries
XLSX: Transitional and Strict; typed records: Ada
XLSX cells: shared strings, formulas, temporal values, disk spill
```

The sample uses this model from a separate project:

```csharp
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class Person
{
    [Column("person_id")]
    public int Id { get; init; }

    [Column("full_name")]
    public required string Name { get; init; }

    public int Age { get; set; }
    public decimal? Balance { get; init; }

    [Ignore]
    public string DisplayName => Name;
}
```

The query uses standard LINQ:

```csharp
var adults = TabularFile.Csv<Person>(path)
    .Where(person => person.Age >= 18)
    .ToList();
```

The file must contain a header:

```csv
person_id,full_name,Age,Balance
1,Ada,36,12.50
2,Grace,17,
```

## Current scope

The library reads paths, supplied streams, and stream factories through sync or async queries.
Each path or factory enumeration opens an independent stream.
Supplied streams permit one enumeration and remain open by default.
Field types include fixed-width numbers, strings, characters, booleans, GUIDs, enums, and date/time values.
The reader can also use nullable value types and a selected conversion culture.

CSV options include delimiter, quote, BOM detection, trimming, blank-line handling, buffers, and length limits.
Async materializers include lists, arrays, and dictionaries.
Shared source and query code remains separate from the CSV adapter for future file formats.
The XLSX reader supports synchronous paths and seekable streams with shared/inline text, rich text, numbers, booleans, dates, and cached formula results.
It has a shared-string memory budget, disk spill, and configurable resource limits.
Async XLSX, factories, and non-seekable input remain planned for stage 3.
See the [XLSX guide](docs/xlsx.md) for conversion and ownership rules.

## Documentation

- [Get started](docs/getting-started.md)
- [Mapping and API reference](docs/reference.md)
- [Value conversions](docs/conversions.md)
- [Sources and async queries](docs/sources-and-async.md)
- [CSV rules and errors](docs/csv-and-errors.md)
- [XLSX reader](docs/xlsx.md)
- [Architecture and contribution](docs/architecture.md)
- [Performance measurements](docs/performance.md)
- [Phase 1 validation](docs/validation-phase1.md)
- [Phase 2 validation](docs/validation-phase2.md)
- [Phase 3 validation](docs/validation-phase3.md)
- [Phase 4 validation](docs/validation-phase4.md)
- [Phase 5 validation](docs/validation-phase5.md)
- [Phase 6 validation](docs/validation-phase6.md)
- [XLSX stage 1 validation](docs/validation-xlsx-stage1.md)
- [XLSX stage 2 validation](docs/validation-xlsx-stage2.md)
- [Release preparation](docs/release.md)
- [Documentation rules](docs/documentation.md)
- [Future phases](docs/spec/plan.md)

## License

The project uses the [Apache License 2.0](LICENSE).
