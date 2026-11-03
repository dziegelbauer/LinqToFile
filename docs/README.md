# User guide

Use LinqToFile to read CSV and XLSX files as typed C# objects.
The library supports standard LINQ, async queries, and Native AOT on .NET 10 and later.

| Task | Guide |
|---|---|
| Install the package and read records | [Get started](getting-started.md) |
| Declare models and map columns | [Mapping and API reference](reference.md) |
| Set CSV syntax, encoding, and limits | [CSV rules and errors](csv-and-errors.md) |
| Create CSV files, append rows, or insert rows | [CSV writing](csv-writing.md) |
| Export CSV to memory, HTTP, or compression streams | [CSV stream exports](csv-streams.md) |
| Select worksheets and read workbook cells | [XLSX workbooks](xlsx.md) |
| Convert values and select a culture | [Value conversions](conversions.md) |
| Map custom types or override a column conversion | [Custom converters](custom-converters.md) |
| Set exact date and time formats per property | [Temporal formats](temporal-formats.md) |
| Read and write custom boolean and null text | [Column tokens](column-tokens.md) |
| Use streams, async queries, and cancellation | [Sources and async queries](sources-and-async.md) |
| Control memory and startup costs | [Performance and memory](performance.md) |
| See unimplemented plans | [Roadmap](roadmap.md) |

Headers are required for both formats.
CSV path sources also support explicit creation, append, and positional insertion.
Invalid file data, missing columns, duplicate headers, and conversion failures throw exceptions.
The format guides describe the error types and location properties.

This is a prerelease library. The API can change before a stable release.
