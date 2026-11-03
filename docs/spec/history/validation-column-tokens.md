# Column token validation

## Scope and local evidence
- BooleanTokens/NullToken annotations, generated span-based mapping, null-first composition, native XLSX boundaries, and private output-collision guards.
- Friendly, benefit-led shared GitHub/NuGet introduction. Detailed header/model/write rules remain in the guides. Five introductory C# blocks remain executable and unchanged.
- Release solution build with warnings as errors: zero warnings/errors. All 1,186 tests pass (baseline 1,140). JIT samples pass, including cross-assembly nullable boolean/string/date annotations, async batches, insertion, collisions, and native numeric XLSX cells.
- Tests cover enum/type/nullability/comparison/whitespace diagnostics; inherited token replacement/conflicts; case, culture independence and trimming; null versus empty strings; nullable enums/temporals; native values and missing cells; escaping/limits; and staged byte preservation on private write errors.
- Warm token matching, boolean conversion, and output-collision checks add zero thread-allocated bytes (minimum of three 10,000-iteration samples after 20,000 warmup calls). This measures those operations, not complete async I/O or owned record/string allocations.
- Project Native AOT publish/run with warnings as errors, repository package Native AOT, and fresh external package consumer under JIT/Native AOT pass on macOS arm64. The external check validates the exact refreshed README and zero runtime package dependencies. Prerelease base: 0.1.0-alpha.16.
- Raw local logs and exit files are under ignored artifacts/tokens-validation*; artifacts/tokens-ide-review.json records the IDE review, including the initial stale diagnostics and subsequent resolution.

## IDE review
- New standing requirement is in AGENTS.md and docs/spec/ide-review.md: always review warnings, suggestions, and hints for every edited file. Evaluate each recommendation and re-inspect changes.
- Rider's initial converter errors referenced an obsolete generator message. IDE rebuild bf70c441-6180-4a8b-87ad-6847a56c22cc succeeded with no problems; reinspection resolved those errors. No diagnostics were suppressed to hide them.
- Applied worthwhile recommendations: clearer annotation-name switch and null/accessibility patterns, simpler comparison patterns, loop instead of array-tail recursion, explicit unused iteration discard, target-typed descriptor creation, async file reads in async token checks, and removal of a redundant expected-array type.
- Retained deliberate conventions: public attributes stay in namespace LinqToFile (moving them to match their folder would break the API); explicit Inherited=true documents the contract; existing private field naming and explicit/local type choices remain consistent with adjacent code.
- Retained branch-style boolean conversion and nearby local helpers for readability; partial scalar-type switch intentionally supports a subset and rejects unsupported types elsewhere. Kept explicit loops rather than adding LINQ enumerators. Kept existing Range.ToArray rather than changing its allocation behavior without evidence.
- Kept source-local expected test arrays and existing sample creation syntax rather than adding static fields or changing runnable examples for style alone. Kept the synchronous insertion/creation checks intentionally alongside async checks. GeneratedRegex adds no material value to this low-volume, cached test-only regex. Exact floating-point comparisons in Program validate exactly representable known constants; tolerance would weaken them. Assertion-only callback use is intentional in tests.
- Existing converter-test collection-expression/async edits were preserved and included in validation.

### File coverage

Rider reports below are for warning severity, including hints. Files outside its project analysis were reviewed manually and are not claimed inspection-clean.

| Files | Review result |
|---|---|
| LinqToFile.Generator/RecordGenerator.cs | Recommendations evaluated; accepted simplifications rechecked; remaining style/subset-switch advice retained as above |
| LinqToFile.Generator/TokenGenerator.cs | Comparison patterns simplified and rechecked; explicit bool retained |
| LinqToFile/Mapping/Attributes.cs | Root namespace and explicit inherited settings intentionally retained |
| LinqToFile/Infrastructure/TokenMapping.cs | Symmetric true/false branches retained for clarity |
| LinqToFile/Infrastructure/TabularValue.cs | Existing field/type conventions retained |
| LinqToFile/Formats/Csv/CsvOutput.cs | Existing naming/type conventions and Range.ToArray retained |
| samples/Models/InventoryItem.cs | No IDE findings reported |
| samples/NativeAot/TokenSample.cs | Async byte reads/pattern cleanup accepted and rechecked; sync insert intentional |
| samples/NativeAot/Program.cs | Exact binary-value comparisons intentionally retained |
| tests/LinqToFile.Tests/TokenGeneratorTests.cs | Explicit type/style hints evaluated and retained |
| tests/LinqToFile.Tests/TokenTests.cs | Async text reads/redundant array type cleanup accepted and rechecked; expected arrays/sync checks intentional |
| tests/LinqToFile.Tests/DocumentationTests.cs | Regex/source-array suggestions evaluated; retained low-volume test implementation |
| tests/LinqToFile.Tests/ConverterTests.cs | Stale errors cleared after IDE rebuild; user edits preserved; remaining test-style hints evaluated |
| AGENTS.md; Directory.Build.props; LinqToFile/LinqToFile.csproj; tests/LinqToFile.Tests/LinqToFile.Tests.csproj; LinqToFile.Generator/AnalyzerReleases.Unshipped.md | No IDE findings reported; edited metadata rechecked |
| README.md; docs/column-tokens.md; docs/conversions.md; docs/csv-writing.md; docs/custom-converters.md; docs/getting-started.md; docs/temporal-formats.md; docs/xlsx.md | No IDE findings reported; human copy reviewed and runnable examples verified |
| docs/README.md; docs/reference.md; docs/roadmap.md | Not included in an IDE project; manually reviewed links and current feature scope |
| docs/spec/column-tokens.md; docs/spec/documentation.md; docs/spec/ide-review.md; docs/spec/maintenance/release.md; docs/spec/mapping.md; docs/spec/plan.md; docs/spec/scope.md; this validation file | Not included in an IDE project; manually reviewed decisions, guidance, and evidence |
| eng/Verify-Package.ps1 | Not included in an IDE project; reviewed edits and executed external JIT/AOT verification |

## Hosted validation
- Existing Windows/Linux/macOS CI runs tests, JIT/project/package/external-consumer Native AOT, and reading/XLSX/writing benchmark matrices. Hosted results are recorded in PR checks after push.
- Open a PR for human review. Do not merge it or enable auto-merge. Main publication remains gated on a human-approved merge and successful main CI.
