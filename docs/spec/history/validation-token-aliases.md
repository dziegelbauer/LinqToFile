# Boolean and null input alias validation

## Scope
- Feature branch: feature/bool-null-aliases, based on origin/main at 60d0e19.
- Add TrueAliases/FalseAliases to BooleanTokens and Aliases to NullToken. Output retains constructor tokens. Null aliases participate in input priority and non-null output collision guards.
- Generator comparisons embed literals. No reflection, per-field arrays, or runtime registration are added. Native XLSX cells retain existing conversion boundaries.

## Local checks
- macOS arm64, .NET SDK 11.0.100, net10.0 and net11.0.
- Release solution build with -warnaserror: zero warnings/errors.
- Release tests: 1,365 passed for each target framework, including generated alias mapping allocation checks, diagnostics, inheritance, CSV sync/async, XLSX text/native boundaries, trimming/comparison, canonical writes, private collision errors, and documentation drift checks.
- Project JIT and project Native AOT samples passed. Clean package consumers for both frameworks passed JIT and Native AOT, package metadata/dependency/license checks, exact README/example checks, and cross-assembly alias reads.
- .NET 11 AOT emitted native SDK debug-map/module-cache warnings and an xcode-select diagnostic about the CommandLineTools selection. Publish exited successfully and native execution passed. These are toolchain diagnostics; managed Release compilation remains warning-free.
- Rider's initial Debug test run failed existing async allocation thresholds. Release CLI runs passed with unchanged thresholds. An initial broad generator assertion incorrectly rejected existing schema arrays; it was removed. Documentation drift failed during concurrent sample edits, then passed after the Release rebuild.
- Sandboxed pack/publish attempts stalled and exited unsuccessfully. Authorized runs outside the sandbox passed.

## IDE recommendation review
- Reviewed the complete branch changes from origin/main, including both new test files, manually. git diff --check passed.
- Rider lint_files completed for Attributes.cs, Infrastructure/TokenMapping.cs, Generator/TokenGenerator.cs, Generator/RecordGenerator.cs, TokenAliasTests.cs, and TokenAliasGeneratorTests.cs. No timeout or incomplete flags were reported. Individual get_file_problems checks completed for samples/Models/InventoryItem.cs and samples/NativeAot/TokenSample.cs after batch lint returned no entries for those files. Both individual checks reported no problems.
- Applied the negated-pattern recommendation in alias validation and re-inspected the affected generator file. Re-inspected the attribute file and final edited sample.
- Retained explicit public root namespaces, inherited attribute flags, explicit local types, and conventional list construction to match existing contracts/style. Generator collection-expression suggestions exceed its pinned C# 10 syntax. Kept explicit loops instead of LINQ to avoid extra enumerator/delegate overhead and keep token validation/emission clear. Kept symmetric early boolean returns rather than a conditional return. Existing enum-switch coverage and local-function style suggestions are unrelated to this change.
- Rider reports missing generated ITabularRecord interfaces and consequent ambiguous Assert.Throws calls in the new test file; Release compilation and both test targets resolve them correctly. Partial declarations are required for generated mappings. The captured XLSX stream is consumed synchronously inside Assert.Throws before its using scope ends. Sync operations in async tests deliberately cover both paths. Collection/var suggestions are stylistic and left consistent with adjacent tests.

## Hosted checks
- Pending feature/PR matrix: Windows x64, Linux x64, macOS arm64, both target frameworks. No merge or publication is performed by this task.
