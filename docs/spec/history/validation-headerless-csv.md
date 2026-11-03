# Headerless CSV validation

## Scope and implementation
- Implements the user-approved contract in ../headerless-csv.md on feature/headerless-csv, based on main after PR #10.
- Generated ColumnIndexes metadata uses a virtual empty default for older mappers. Effective indexes use nearest-override inheritance; LTF008 rejects negative/duplicate indexes. Completeness is checked when selecting index mode.
- Shared query/schema binding handles supplied names, first-record index widths, empty input, one-based logical diagnostics, and reusable row metadata. Only CSV sources expose the new configuration. Existing write and XLSX contracts remain intact.
- Supplied names are copied and validated immediately, including on comparison/trimming changes. Per-enumeration schema arrays do not grow with record count.
- An out-of-range int.MaxValue annotation produces a schema error with TargetMember but no ColumnIndex: the existing nullable int one-based diagnostic cannot represent 2,147,483,648. No index-sized allocation occurs.

## Local checks (macOS arm64)
- Release solution build with -warnaserror: zero warnings/errors.
- Full suite: 1,336 tests pass, none skipped. The final test-only UTF-8 literal/async-disposal cleanup was followed by 39 passing headerless/documentation tests.
- Allocation guard now covers header-based, supplied-name, and index reading, synchronously and asynchronously. Fixed-storage sources compare 10,000 with 1,000,000 rows; excess growth beyond owned model allocations remains within the unchanged 4,096-byte bound.
- Project JIT and Native AOT samples pass, including a separately compiled HeaderlessEntry and both mapping modes. Async-only factories and tiny buffers are exercised.
- LinqToFile 0.1.0-alpha.20 packs successfully. Verify-Package.ps1 passes with a fresh external two-project consumer and package cache, under both JIT and Native AOT. It checks packaged generator/runtime payload, empty runtime dependencies, Apache-2.0 metadata/license, exact README text, and executable README/examples.
- Focused tests cover configuration snapshots/transitions/validation, missing indexes, first-record data, Unicode/multiline/tiny buffers, skipped-field syntax/limits, widths/trailing fields/bounds, empty/skipped input, first-record culture/tokens/temporal annotations, privacy/locations, non-seekable ownership/single use, cancellation/early exit/failure cleanup, named/XLSX mapping, and path creation/append/insertion preservation.
- Human guides were manually reviewed for current usage, short sentences, and active voice. Documentation drift checks verify all three headerless examples against runnable sources. No STE certification is claimed.
- git diff --check passes. Hosted Windows/Linux/macOS validation is separate from these local results.

## Rider review
- Rider build completed successfully with no retained build problems.
- lint_files at warning severity (including suggestions/hints) completed for all edited C# files: Attributes, RecordMapper, RecordGenerator, TabularOptions, HeaderlessSchema, TabularQuery, CsvSource, CsvFile, CsvRecordReader, TabularRow, TabularFileException, HeaderlessGeneratorTests, HeaderlessCsvTests, StreamingAllocationTests, DocumentationTests, HeaderlessEntry, HeaderlessCsvSample, and Program. No batch reported timeout or incomplete analysis.
- Accepted and rechecked: removal of a redundant namespace qualifier; span collection expression in index binding; UTF-8 literals and async disposal in new tests/sample.
- Retained public namespaces despite folder-name suggestions to preserve the API. Retained explicit Inherited=true to state the annotation contract consistently. Existing private-field names, explicit scalar types, equivalent syntax/pattern/var preferences, and generator loop/local-function style remain consistent with surrounding code.
- Retained direct loops in allocation tests to measure the existing streaming contract without adding LINQ iterator/delegate costs. Retained synchronous write/read/cancellation calls where tests deliberately cover sync/async parity and cancellation boundaries. Explicit CsvFile typing verifies fluent return types. Exact floating-point comparisons in the existing sample use exactly representable fixtures.
- Existing documentation regex/static-array hints and unrelated TabularRow pattern-style suggestions do not affect this feature; changing them would add unrelated churn. Existing scalar conversion switch fallbacks deliberately handle unsupported enum cases.
- eng/Verify-Package.ps1 could not be analyzed by Rider because it is not included in a solution project. Its edited loops/sample invocation were manually reviewed and the complete script ran successfully through fresh JIT/Native AOT consumers. This file is not claimed inspection-clean. Markdown/project metadata is exempt from code inspection.
