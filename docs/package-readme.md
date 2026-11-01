# LinqToFile

Read CSV files as typed C# objects with standard LINQ.
An experimental XLSX foundation is also included.
The minimum target is .NET 10. The runtime uses only the BCL.
The package includes a source generator. Runtime mapping uses no reflection.
Native AOT is supported.

This is a prerelease package. The API can change before a stable release.

## Read a CSV file

Add the package to each project that declares mapped models.
Use a public partial class with a public parameterless constructor.
The constructor can be implicit.

```csharp
using LinqToFile;

[TabularRecord]
public sealed partial class Person
{
    [Column("person_id")]
    public int Id { get; init; }
    public required string Name { get; init; }
}
```

For this input:

```csv
person_id,Name
1,Ada
2,Grace
```

Read records with this query:

```csharp
var people = TabularFile.Csv<Person>("people.csv")
    .Where(person => person.Id > 0)
    .ToList();
```

Properties without `[Column]` use their property names as headers.
Public setters and init accessors are supported.
Header comparison is ordinal and case-sensitive by default.
Missing columns, duplicate headers, malformed CSV, and conversion errors throw exceptions.

## Stream records

Use `foreach` or `await foreach` to read records incrementally.
Paths and stream factories open independent streams for each enumeration.
Supplied streams permit one enumeration and remain open by default.
Options include `WithEncoding`, `WithCulture`, and `WithBufferSize`.
Async materializers include `ToListAsync`, `ToArrayAsync`, and `ToDictionaryAsync`.

The XLSX foundation supports synchronous paths and seekable streams with simple inline text, numbers, and booleans.
It rejects shared strings, formulas, rich text, and native date cells in this stage.
Many ordinary Excel workbooks require these planned features.
Async XLSX and non-seekable input are also planned.
See the [XLSX guide](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/xlsx.md) for current limits.
Headerless input and other formats remain future objectives.
Custom converters and per-column error recovery are also future objectives.

## Documentation and license

- [Get started](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/getting-started.md)
- [API reference](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/reference.md)
- [Sources and async queries](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/sources-and-async.md)
- [Conversions and errors](https://github.com/dziegelbauer/LinqToFile/blob/main/docs/conversions.md)
- [Apache License 2.0](https://github.com/dziegelbauer/LinqToFile/blob/main/LICENSE)
