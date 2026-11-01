# LinqToFile

Read CSV and XLSX files as typed C# objects with standard LINQ.
Use sync or async queries to stream records or create lists, arrays, and dictionaries.

The library targets .NET 10 and later.
The runtime uses only the .NET Base Class Library (BCL), with no runtime reflection.
The package includes a source generator and supports Native AOT.

This is a prerelease library. The API can change before a stable release.

## Read a CSV file

[Install the package](docs/getting-started.md), then declare a public partial model.
Each project that declares models must reference the package.

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

Use `TabularFile.Xlsx<T>()` to read a workbook with the same model annotations.
See the [XLSX guide](docs/xlsx.md) for worksheet selection, cell conversions, and resource limits.

## Documentation

Start with the [user guide](docs/README.md).

- [Get started](docs/getting-started.md)
- [Mapping and API reference](docs/reference.md)
- [CSV rules and errors](docs/csv-and-errors.md)
- [XLSX workbooks](docs/xlsx.md)
- [Value conversions](docs/conversions.md)
- [Sources, async queries, and cancellation](docs/sources-and-async.md)
- [Performance and memory](docs/performance.md)
- [Roadmap](docs/roadmap.md)

## License

The project uses the [Apache License 2.0](LICENSE).
