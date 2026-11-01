# Mapping

## Confirmed
- Entry point shape: TabularFile.Csv<T>(), using a user-supplied class.
- Require a [TabularRecord] model marker for reliable source-generator discovery, including usage through generic helpers. Per-column annotations remain optional.
- Mapped classes must be partial. The generator adds a static ITabularRecord<T> mapping contract, avoiding runtime registration and reflection. Nested model containing types must also be partial so generated declarations can reach the model.
- Initial models are public, non-abstract, non-generic classes, including public nested classes. Generic and non-public model classes are deferred; nested model accessibility must be effective through its containing types.
- Define an annotation to supply a column header.
- Support public setters and init-only setters.
- Require a public parameterless constructor initially.
- Header matching defaults to ordinal, case-sensitive comparison and is configurable.
- Initial supported header comparisons: StringComparer.Ordinal and StringComparer.OrdinalIgnoreCase only.
- Reject multiple properties mapped to the same header under the configured comparison.
- Compile-time source-generator tooling dependencies are allowed, isolated from the BCL-only runtime library.
- Properties without a column annotation map by property name.
- Throw if a mapped column is missing from the header.
- Ignore extra file columns with no mapped property.
- Throw for duplicate header names under the configured header comparison, including unmapped headers.
- Preserve empty string fields as empty strings, including for nullable string properties.
- Empty fields map to null for nullable value types and throw for non-nullable value types.
- Built-in conversions default to InvariantCulture, configurable per file.
- Reject empty header names, including for unmapped columns.
- Reject whitespace-only header names. Preserve whitespace in meaningful headers unless header trimming is explicitly enabled.
- Header trimming uses .NET whitespace rules before header validation, duplicate detection, and property matching. Annotation names remain literal and are not trimmed.
- For overrides, the most-derived mapping annotation takes precedence within its kind; otherwise inherit the nearest base annotation. Reject effective mappings containing both Column and Ignore.
- Cross-kind override conflicts are compiler errors: Ignore on an override conflicts with inherited Column, and Column on an override conflicts with inherited Ignore. An overriding Column may replace an inherited Column.
- Use [Column("header")] for explicit header mapping and [Ignore] to exclude properties.
- Null, empty, or whitespace-only Column annotation names produce compiler diagnostics. Meaningful annotation names preserve their exact whitespace.
- Public models in separate projects are supported when the model project references the package and generates its mappings. Callers do not register those mappings manually.
- Support C# required properties through generated initializers assigning every required member. Unsupported model shapes produce compiler diagnostics.
- Ignored required properties produce compiler errors initially; no explicit default-value mechanism is planned for the first pass.
- Initial built-in conversions: string, char, bool, numeric primitives, decimal, Guid, enums, DateTime, DateTimeOffset, DateOnly, TimeOnly, and TimeSpan, plus applicable nullable forms.
- Unsupported conversion types produce compiler diagnostics initially; custom conversion is deferred.
- Boolean conversion accepts only true and false, ignoring case. Other tokens such as 0/1 and yes/no are deferred to custom conversion.
- Quoted and unquoted empty fields have identical conversion semantics. Special null tokens are deferred.
- Enum conversion accepts case-sensitive names and comma-separated names for flags; reject numeric tokens and undefined values.
- Each enum input token must be a declared name. Combinations of declared flags are allowed even if the combined numeric value has no separately named constant.
- Integer conversion accepts a sign and surrounding whitespace. Floating-point conversion additionally accepts decimal points and exponents; decimal conversion additionally accepts decimal points. Reject thousands separators and currency symbols by default, using the configured culture for applicable syntax.
- Reject non-finite floating-point values (NaN and positive/negative infinity), including non-finite results from overflow.
- Date/time conversion initially uses each type's BCL parsing with the configured culture, without implicit conversion to the machine's local time zone. Exact parsing styles and timezone-less input semantics require implementation review.
- DateTimeOffset input requires an explicit offset; otherwise throw. Offset-free DateTime values retain DateTimeKind.Unspecified.
- Reject offset-bearing input for DateTime properties initially; use DateTimeOffset for offset-bearing data rather than implicitly normalizing or discarding offsets.
- A char field must contain exactly one UTF-16 code unit; longer or empty non-nullable char fields throw.
- Initial mapped data members are properties only. Field mapping is deferred; required fields make a model unsupported and produce compiler diagnostics.
- Materialized records own their values and remain safe to retain after enumeration advances or ends. String properties require owned strings; avoid temporary per-field strings for numeric conversion.
- Map inherited public writable properties. Ignore public read-only properties and indexers unless explicitly annotated for mapping, in which case emit compiler diagnostics.
- Map overridden properties once, using the most-derived declaration. Hidden properties that create ambiguous mappings produce compiler diagnostics.
- Combining [Column] and [Ignore] on one property produces a compiler error. Properties annotated for mapping without a public setter or public init accessor also produce compiler errors.
- Avoid runtime reflection; use compile-time source generation for typed mapping.
- Generated mapping must be compatible with Native AOT and minimize allocations.

## Proposed, not approved
- Format-independent annotations.

## Unresolved
- Complete conversion implementations in phase 3; agreed policies above remain the target.
- Additional model shapes beyond phase 1. Parameterized constructor mapping is deferred.
- Validate further inheritance edge cases as the generator expands.

## Future objectives
- Configurable missing-column handling.
- Custom converters, additional boolean tokens, and explicit null tokens.
- Per-column converter annotations; custom conversion remains outside the initial release.
- Exact date/time format annotations.
