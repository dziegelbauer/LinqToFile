# Architecture and contribution

## Projects

| Project | Responsibility |
|---|---|
| `LinqToFile` | Shared query and mapping code, with a CSV adapter |
| `LinqToFile.Generator` | Compiler diagnostics and typed mapper generation |
| `LinqToFile.Tests` | Format, source, query, and generator tests |
| `Models` | Sample models in a separate assembly |
| `NativeAot` | Executable sample for JIT, package, and Native AOT checks |
| `LinqToFile.Benchmarks` | Repeatable measurements of typed streaming |

The runtime targets .NET 10.
The generator targets .NET Standard 2.0 for compiler host compatibility.
Roslyn dependencies belong to the generator and tests.
The single consumer package includes the generator without runtime package dependencies.

## Runtime layout

| Folder | Responsibility |
|---|---|
| `Sources` | Stream acquisition, single-use claims, and disposal leases |
| `Reading` | Shared options, text decoding, buffering, reader contracts, and schema binding |
| `Queries` | Sync/async enumeration and common materialization |
| `Formats/Csv` | CSV options, parser, reader adapter, fluent source, and format exception |
| `Mapping` | Model attributes and the static mapping interface |
| `Infrastructure` | Generated mapper contract and reusable `TabularRow` |
| `Errors` | Shared exception types |

The public entry point remains `TabularFile`.
Public CSV source and exception namespaces remain `LinqToFile`.
Folders separate responsibilities without a separate package for each layer.

## Source lifetime

A source descriptor opens a file or calls a factory on the first enumeration step.
A stream lease controls sync or async disposal.
All configurations for one supplied stream share an atomic claim.
Format readers do not own the source stream.

A path or factory can open independent streams for concurrent enumerations.
A supplied-stream source permits one enumeration across both modes.
Cancellation and failure do not restore its allowance.

## Format boundary

A format factory returns an internal `IRecordReader`.
That reader supplies rows, sync/async read operations, and format-specific width errors.
Shared query code handles source lifetime, header binding, and typed mapping.
It does not reference CSV syntax or CSV option types.

A synthetic byte-format test uses the shared query engine.
It supplies an implicit header and one value per byte.
The test passes in both sync and async modes without the CSV parser.

This internal boundary prepares the library for more formats.
It is not a public provider API.
A second real format must validate that API before publication.

`TabularRow` currently stores text-backed cells.
Its typed readers and empty-value check isolate generated mappers from parsing.
Future formats can extend cell representation without another source-lifetime implementation.

## Generated mapping

The generator finds classes with `[TabularRecord]`.
It checks each class and makes a mapper with direct property assignments.
It also adds an explicit static interface implementation to the partial class.
The runtime gets the mapper through `T.Mapper`.

This contract makes a direct dependency that Native AOT can keep.
It avoids reflection, global registration, and application startup hooks.
Models from another assembly use the same contract.
Nested models must have partial containing types.

Generated column and member names supply schema diagnostics without reflection.
Name arrays share storage when their contents are identical.
Numeric readers use BCL generic math over character spans.
Enum readers compare declared names and combine constant flag values without token strings.

Temporal readers use the BCL date/time parsers.
An offset probe uses `DateTimeStyles.AdjustToUniversal` without an assumed time zone.
Offset-free results remain unspecified under that style.
The reader rejects offset-bearing DateTime inputs and keeps explicit DateTimeOffset offsets.
The [BCL style reference](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.datetimestyles?view=net-10.0) describes the probe behavior.

## CSV state machine

The CSV parser has one state machine for sync and async reads.
It consumes available characters without I/O.
A synchronous reader refills the buffer with `Read`.
An asynchronous reader uses `ReadAsync` only when the buffer is empty.

Quote, CRLF, field, and record state survive a refill.
Cancellation checks also apply to buffered records.
The decoder replays at most four prefix bytes for BOM selection without a seek.

## Record ownership and queries

Each enumeration owns a decoder, parser, and reusable row buffer.
Character and field-range arrays grow with the largest active record.
The mapper makes an owned model object for each returned record.
Strings also own their data.

The configured CSV source implements `IEnumerable<T>`.
It also supplies the async-foreach pattern.
`AsAsyncEnumerable()` selects the BCL async query interface explicitly.
This boundary preserves direct synchronous LINQ without ambiguous operators.

List and array materializers delegate to the BCL.
The shared dictionary builder uses `TryAdd`.
Its duplicate-key message omits raw keys.
Explicit BCL operators and user callbacks keep their own error behavior.

## Contribution checks

The validation records give results and reproduction commands:
[phase 1](validation-phase1.md), [phase 2](validation-phase2.md),
[phase 3](validation-phase3.md), and [phase 4](validation-phase4.md).

1. Update the relevant specification when a decision changes.
2. Add tests for new behavior.
3. Update the human documentation.
4. Run the examples affected by the change.
5. Run the unit tests.
6. Run the AOT sample after changes to mapping or dependencies.

The documentation uses [ASD-STE100 Issue 9](documentation.md).
The release license is Apache-2.0.
