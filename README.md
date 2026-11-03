# LinqToFile

Query CSV and XLSX files with standard LINQ and typed C# models.
Create CSV files, append rows, or insert rows at a selected position.

Use LinqToFile for data imports, reports from files, and CSV exports.
Map headers to properties once, then work with C# values throughout your application.

## Why use it?

- Use the same model annotations for CSV and XLSX.
- Process records one at a time, or collect results in a list, array, or dictionary.
- Select sync or async I/O to suit your application.
- Configure encodings, CSV syntax, culture, and size limits per source.
- Map domain types or custom column values with [converter annotations](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/custom-converters.md).
- Select [exact date and time formats](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/temporal-formats.md) per property for text input and CSV output.
- Publish with Native AOT. Runtime mapping uses no reflection.
- Keep deployment dependencies small: the runtime uses only the .NET BCL.

The minimum target is .NET 10. Headers are required.

| Format | Read | Write |
|---|---|---|
| CSV | Sync/async streams and collections | Create, append, and positional insertion; single rows and ranges |
| XLSX | Sync/async streams and collections, with worksheet selection | Not supported |

This is a prerelease library. The API can change before a stable release.

## Install

From your application project directory, run:

```sh
dotnet add package LinqToFile --prerelease
```

The package includes the source generator.
Add the package to each project that declares mapped models.
See [Get started](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/getting-started.md) for local packages and Native AOT setup.

## Declare a model

Put the model in a public partial class with a public parameterless constructor.
The constructor can be implicit.
Public setters and init accessors are supported.
Writing also requires an accessible getter.

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

`Column` changes a header mapping. Properties without it use their property names.
`Ignore` excludes a property.
Add `using LinqToFile;` and `using SampleModels;` to the query file.

## Read and filter CSV

For a CSV file at `path`:

```csv
person_id,full_name,Age,Balance
1,Ada,36,12.50
2,Grace,17,
```

Select adults with ordinary LINQ:

```csharp
var adults = TabularFile.Csv<Person>(path)
    .Where(person => person.Age >= 18)
    .ToList();
```

Use `foreach` to stream records.
Use `AsAsyncEnumerable()` and `await foreach` for async streaming.
To collect records by key:

```csharp
var peopleById = await TabularFile.Csv<Person>(path)
    .ToDictionaryAsync(person => person.Id);
```

Set options such as `WithEncoding`, `WithDelimiter`, or `WithCulture` before applying LINQ operators.

## Create, append, and insert CSV rows

Set `outputPath` to a new file path.
Creation rejects an existing destination.
Append and insertion require an existing file.

```csharp
var file = TabularFile.Csv<Person>(outputPath);
await file.CreateAsync(new[]
{
    new Person { Id = 1, Name = "Ada", Age = 36 }
});
await file.AppendAsync(new Person { Id = 2, Name = "Grace", Age = 17 });
await file.InsertAsync(1, new Person { Id = 3, Name = "Katherine", Age = 40 });
```

The insertion position is a zero-based data-row index.
Range methods accept `IEnumerable<T>`; async range methods also accept `IAsyncEnumerable<T>`.
Write methods return the number of data rows written.

By default, writes prepare a temporary result before publishing or replacing the destination.
Use `WithInPlaceWrites()` when you accept partial changes on failure.
See [CSV writing](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/csv-writing.md) for validation, quick append, cancellation, and encoding rules.

## Read an XLSX worksheet

The same model can read matching workbook headers.
Set `workbookPath` to the workbook path:

```csharp
var workbookAdults = TabularFile.Xlsx<Person>(workbookPath)
    .WithWorksheet("People")
    .Where(person => person.Age >= 18)
    .ToList();
```

XLSX reading supports shared strings, native cell values, and saved formula results.
It does not recalculate formulas.
See [XLSX workbooks](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/xlsx.md) for supported cells, startup work, and temporary storage.

## Learn more

- [User guide](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/README.md)
- [Mapping and API reference](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/reference.md)
- [CSV syntax and errors](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/csv-and-errors.md)
- [Value conversions](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/conversions.md)
- [Sources, async queries, and cancellation](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/sources-and-async.md)
- [Performance and memory](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/performance.md)
- [Roadmap](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/roadmap.md)
- [Apache License 2.0](https://github.com/dziegelbauer/LinqToFile/blob/main/LICENSE)
