# LinqToFile

Query CSV and XLSX files with the LINQ you already know.

LinqToFile turns file data into typed C# objects, ready to filter, sort, group, and transform.
Use it to import records, build reports, or export CSV data.
Define your model once, then work with your data through familiar C# code.

## Why use it?

- **Keep your queries familiar.** Use standard LINQ with strongly typed records from CSV or XLSX files.
- **Choose how much to load.** Stream records or collect the results you need in a list, array, or dictionary.
- **Write as well as read.** Export typed records to Excel workbooks, create or extend CSV files, and send output to streams.
- **Work with real-world data.** Select encodings, cultures, date formats, and boolean or null tokens for your files.
- **Use your own types.** Add custom converters when your application needs its own value rules.
- **Keep deployment simple.** Publish with Native AOT and no extra runtime package dependencies.

The same model annotations work across CSV and XLSX, so you can reuse your mapping as your data sources change.

## Install

For .NET 10 or later, add the prerelease package to your model project:

```sh
dotnet add package LinqToFile --prerelease
```

See [Get started](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/getting-started.md) for project setup and Native AOT builds.

## Declare a model

Describe the records you want to work with.
Use `[Column]` when a file column has a different name from your C# property.

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

Properties use their own names by default. `[Ignore]` leaves a property out of the mapping.
Add `using LinqToFile;` and `using SampleModels;` to the query file.

## Read and filter CSV

Suppose your CSV file at `path` contains these people:

```csv
person_id,full_name,Age,Balance
1,Ada,36,12.50
2,Grace,17,
```

Select the adults with a familiar LINQ query:

```csharp
var adults = TabularFile.Csv<Person>(path)
    .Where(person => person.Age >= 18)
    .ToList();
```

Use `foreach` to process records as they arrive, or `await foreach` with `AsAsyncEnumerable()` for async streaming.
You can also collect records by key:

```csharp
var peopleById = await TabularFile.Csv<Person>(path)
    .ToDictionaryAsync(person => person.Id);
```

Chain options such as `WithEncoding`, `WithDelimiter`, or `WithCulture` before your LINQ query to suit your data.

## Create, append, and insert CSV rows

Start with a new file at `outputPath`, then add rows as your data arrives:

```csharp
var file = TabularFile.Csv<Person>(outputPath);
await file.CreateAsync(new[]
{
    new Person { Id = 1, Name = "Ada", Age = 36 }
});
await file.AppendAsync(new Person { Id = 2, Name = "Grace", Age = 17 });
await file.InsertAsync(1, new Person { Id = 3, Name = "Katherine", Age = 40 });
```

Range methods let you write batches from ordinary or async sequences.
See [CSV writing](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/csv-writing.md) for more examples and write options.
For HTTP responses, memory, or compression, use [CSV stream exports](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/csv-streams.md) with `TabularFile.CsvWriter<T>(stream)`.
You can also send your results to Excel with `TabularFile.XlsxWriter<Person>(outputPath).WriteAsync(people)`. See [XLSX writing](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/xlsx-writing.md) for path and stream examples.

## Read an XLSX worksheet

Switch to a workbook at `workbookPath` and choose a worksheet. Your model and LINQ query stay familiar:

```csharp
var workbookAdults = TabularFile.Xlsx<Person>(workbookPath)
    .WithWorksheet("People")
    .Where(person => person.Age >= 18)
    .ToList();
```

See [XLSX workbooks](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/xlsx.md) for worksheet selection and cell conversion options.

## Learn more

- [User guide](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/README.md)
- [Mapping and API reference](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/reference.md)
- [CSV syntax and errors](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/csv-and-errors.md)
- [Value conversions](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/conversions.md)
- [Date and time formats](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/temporal-formats.md)
- [Boolean and null tokens](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/column-tokens.md)
- [Custom converters](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/custom-converters.md)
- [Sources, async queries, and cancellation](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/sources-and-async.md)
- [Performance and memory](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/performance.md)
- [Roadmap](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/roadmap.md)
- [Apache License 2.0](https://github.com/dziegelbauer/LinqToFile/blob/main/LICENSE)
