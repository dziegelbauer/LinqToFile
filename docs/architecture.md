# Architecture and contribution

## Projects

| Project | Responsibility |
|---|---|
| `LinqToFile` | Public API, CSV reader, schema binding, and conversion |
| `LinqToFile.Generator` | Compiler diagnostics and typed mapper generation |
| `LinqToFile.Tests` | Reader tests and generator compilation tests |
| `Models` | Sample models in a separate assembly |
| `NativeAot` | Executable sample for JIT, package, and Native AOT checks |
| `LinqToFile.Benchmarks` | Repeatable measurements of typed streaming |

The runtime targets .NET 10.
The generator targets .NET Standard 2.0 for compiler host compatibility.
Roslyn dependencies belong to the generator and tests.
The consumer package does not expose those dependencies to the application runtime.

## Generated mapping

The generator finds classes with `[TabularRecord]`.
It checks each class and makes a mapper with direct property assignments.
It also adds an explicit static interface implementation to the partial class.
The runtime gets the mapper through `T.Mapper`.

This contract makes a direct dependency that Native AOT can keep.
It avoids reflection, global registration, and application startup hooks.
Models from another assembly use the same contract.
Nested models must have partial containing types.

## Record ownership

Each enumeration owns a file reader and a reusable record buffer.
The parser stores characters and field ranges in arrays.
The arrays grow when necessary.
Numeric conversion reads spans over that buffer.

The mapper makes one owned model object for each record.
String properties receive owned strings.
A model remains valid after the parser reads another record.
The query API returns only owned values.

## Query execution

The source uses the `IEnumerable<T>` interface.
It opens the file on the first enumeration step.
It reads and checks the header before it returns a data record.
It binds property columns to file ordinals once per enumeration.
Standard LINQ controls later query operations.

## Phase 1 limits

The parser is a synchronous foundation.
It uses buffered `StreamReader` input with character-by-character parser steps.
Buffer controls and field limits are not available yet.
Later performance work can change this implementation without a change to record ownership.

The format boundary is not a public extension API yet.
A later tabular reader can reuse the generated mapping contract.
A second format must test that design before the project adds a provider API.

## Contribution checks

The [phase 1 validation record](validation-phase1.md) gives the initial results and reproduction commands.

1. Update the relevant specification when a decision changes.
2. Add tests for new behavior.
3. Update the human documentation.
4. Run the examples affected by the change.
5. Run the unit tests.
6. Run the AOT sample after changes to mapping or dependencies.

The documentation uses [ASD-STE100 Issue 9](documentation.md).
The release license is Apache-2.0.
