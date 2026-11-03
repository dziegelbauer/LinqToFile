# Per-column custom converters

## Authorized objective
- Implement custom converters with per-property annotations, following the user's selected roadmap item.
- Support CSV/XLSX reading and CSV writing through shared generated mapping.
- Use span-based parsing/formatting and direct generated calls. Preserve .NET 10+, Native AOT, a BCL-only runtime, and minimized allocations.
- Unannotated properties retain existing conversions. Keep existing exception/location and sensitive-data rules.
- Update human usage documentation and runnable examples, validate package/cross-assembly Native AOT and CI, then open a PR for review.
- Stop for unresolved public behavior questions. Commit all changes before handing work back. All main merges require human approval.

## Confirmed converter input
- Existing XLSX mapping distinguishes text, native numeric/boolean values, and ISO dates. Built-in readers apply cell-kind boundaries and special numeric date-system rules.
- Custom converters can inspect native XLSX cells as well as text, receiving a value span plus cell kind, culture, and applicable date-system context. CSV values remain text. Do not implicitly convert every native value into culture-formatted text. The user approved this behavior.

## Confirmed empty fields and null values
- An explicitly annotated converter receives empty input fields and null property values, and owns their interpretation. This permits custom null tokens and empty-field defaults without another option. Unannotated properties retain existing empty/null behavior. The user approved this behavior.

## Confirmed converter failures
- Follow BCL TryParse/TryFormat conventions. Parsing returns false for invalid input. Formatting returns false only when its destination span is too small; the library grows reusable storage and retries. A converter throws for a value it cannot format.
- Failed parsing and converter exceptions become the existing location-aware conversion/write exceptions, without raw values or inner exceptions. Keep I/O, enumeration, and cancellation outside converter callbacks under their existing rules.
- The user approved this contract.

## Implementation contract
- ColumnConverterAttribute(Type) selects an accessible, closed, non-abstract, non-file-local class or struct implementing IColumnConverter<T> for the complete property type, including nullability. No constructor requirement or converter instance; explicit static implementations are supported.
- Static TryParse(ReadOnlySpan<char>, ColumnConversionContext, out T) and TryFormat(T, Span<char>, ColumnConversionContext, out int) use constrained static interface dispatch from generated ReadConverted/SetConverted calls.
- Context is a readonly struct: Culture is the configured read-only snapshot; CellKind is TabularCellKind Text/Number/Boolean/IsoDate; DateSystem is TabularDateSystem None/Excel1900/Excel1904. CSV reads/writes use Text/None. XLSX uses native lexical values (invariant numbers, normalized booleans) and actual workbook date system, without style formatting or implicit culture conversion.
- Field trimming precedes the callback. Returned records own their values. Converters may not retain spans; formatting can retry, and callbacks must support concurrent enumerations.
- LTF005 diagnoses invalid/inaccessible/mismatched converters. Nullable reference annotation mismatch is diagnosed; typeof erases annotations on generic reference arguments, so prefer a dedicated nullable-reference implementation. Normal C# interface implementation errors remain compiler errors.
- Override chains inherit the nearest converter independently of Column; a more-derived converter replaces it. Effective Ignore plus converter is LTF004. Annotated non-writable/static/indexer properties are LTF002. Setter-only reading remains supported; writing still rejects inaccessible getters before file changes.
- Converter parsing false/throws use private location-aware conversion errors. Formatting false retries reusable storage, success checks output count, and throws use existing private write errors. OutOfMemoryException is not wrapped. Cancellation thrown inside callbacks is a conversion failure; normal operation cancellation/IO/input enumeration rules remain outside callbacks.
- Dedicated built-in format/token annotations and unrelated roadmap features remain out of scope.

## Validation
- Focused generator/runtime tests cover direct dispatch, domain/scalar/nullable types, explicit implementations, constructors, inheritance/accessibility/diagnostics, trimming, native XLSX kinds/date systems, culture, failures/privacy, buffer growth/output counts, limits, and zero per-value allocation after warmup.
- Product/ConvertedStock models and ConverterSample exercise cross-assembly CSV writes and CSV/XLSX reads under the existing JIT, project Native AOT, and fresh package-consumer checks on all CI platforms.
- Full local and hosted validation evidence is recorded in history/validation-converters.md when complete.
