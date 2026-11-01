# LinqToFile

LinqToFile reads CSV files as typed C# records. Standard LINQ operators filter and select the records.
The runtime uses only the .NET Base Class Library (BCL). The minimum target is .NET 10.

This repository contains the phase 1 foundation. The API can change before a stable release.
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

Phase 1 reads file paths through synchronous queries.
Each enumeration opens a new file and closes it after use.
The current field types are `string`, `int`, `decimal`, `bool`, and `Guid`.
The reader can also use nullable value types.

Async queries, stream inputs, more conversions, and additional CSV options are planned.
These features are not available in phase 1.

## Documentation

- [Get started](docs/getting-started.md)
- [Mapping and API reference](docs/reference.md)
- [CSV rules and errors](docs/csv-and-errors.md)
- [Architecture and contribution](docs/architecture.md)
- [Performance measurements](docs/performance.md)
- [Phase 1 validation](docs/validation-phase1.md)
- [Documentation rules](docs/documentation.md)
- [Future phases](docs/spec/plan.md)

## License

The project uses the [Apache License 2.0](LICENSE).
