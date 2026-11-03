# Exact temporal format annotations

## Authorized objective
- Implement per-property exact date/time formats for DateTime, DateTimeOffset, DateOnly, and TimeOnly, including nullable forms.
- Apply shared mapping to CSV/XLSX reading and CSV output; preserve BCL-only runtime, Native AOT, no runtime reflection, and minimized allocations.
- Preserve existing conversions for unannotated properties. Define explicit culture/timezone behavior and compiler diagnostics for conflicting annotations.
- Update human usage guides and runnable examples; validate tests, allocations, package consumers, Native AOT, and hosted CI. Commit and push all work and open a PR for human review. Never merge it automatically.
- Stop for unresolved public behavior questions and ask one item at a time.

## Confirmed format scope
- One TemporalFormat annotation specifies one .NET format string used both for exact text parsing and CSV formatting, for example [TemporalFormat("yyyy-MM-dd")]. No fallback to general parsing for annotated text fields. The user approved this behavior.
- Multiple accepted input formats or distinct read/write formats remain future objectives.

## Confirmed native XLSX cells
- Apply the format to CSV fields and XLSX text cells, and to CSV output. Native XLSX numeric serials and ISO date cells keep existing target-type/date-system rules, independent of the annotation. Do not render native cells into display text before parsing. The user approved this behavior.

## Confirmed custom converter conflicts
- Reject an effective mapping containing both TemporalFormat and ColumnConverter with a compiler diagnostic, including combinations inherited through overrides. A converter already owns parsing/formatting; silently prioritizing either annotation would hide a configuration error. The user approved this behavior.

## CI interruption and resumption
- The user explicitly paused this branch to fix the failed main CI run after PR #4 and explicitly authorized merging that fix. This does not authorize merging the temporal-format PR.
- PR #5 merged as 30fbc34 after both branch and PR Windows/Linux/macOS matrices passed. Main run 41555256955 passed all platform checks and NuGet publication. This branch was rebased onto that fix, preserving all format decisions, and contract work has resumed.

## Confirmed culture
- Annotated formats use the configured WithCulture snapshot for both exact text parsing and CSV formatting, defaulting to InvariantCulture. Unannotated temporal formatting retains its existing invariant round-trip behavior; native XLSX conversion remains unchanged. The user approved this behavior.

## Existing timezone requirements to preserve
- Existing mapping/writing requirements still apply: DateTime accepts offset-free input and writes only Unspecified values; DateTimeOffset requires an explicit offset and preserves that offset and clock time. Do not infer or convert through the machine's local timezone. Exact-format validation must account for these existing requirements rather than silently bypass them.

## Confirmed complete dates
- Formats for DateTime, DateTimeOffset, and DateOnly must specify a complete date (year, month, day). Reject formats that require the BCL to fill missing date components. Time-only data should use TimeOnly; omitted seconds/fractions can retain zero defaults. The user approved this restriction.

## Confirmed intentionally omitted output components
- Selecting a format explicitly permits omission of time components or fractional seconds from output. For example, TimeOnly with HH:mm writes 14:30 even when the value also has seconds. Do not reject a value merely because its omitted components are nonzero; document that reading the result cannot recover those components. Existing date-completeness and timezone/offset requirements still apply. The user approved this behavior.

## Implementation choices
- TemporalFormatAttribute(string) is a literal user-provided .NET format, not a magic string for a fixed option. The generated mapper emits direct ReadDateTime/DateTimeOffset/DateOnly/TimeOnlyExact calls and typed TabularValue.Set overloads with the format literal.
- LTF006 diagnoses unsupported types, null/blank/malformed/incomplete formats, invalid type-specific components, and offset-dropping/normalizing formats. LTF004 covers effective Ignore/ColumnConverter conflicts, including override chains. The nearest same-kind override annotation wins; Column can rename an annotated property. Setter/init/getter restrictions stay unchanged.
- Shared format analysis source compiles into both the netstandard2.0 generator and .NET 10 runtime. The generator also runs a fixed invariant BCL formatting/parsing syntax probe, expanding DateOnly/TimeOnly O/R portions for the older target. This is not a guarantee that every value/culture/calendar can use the format.
- Standard formats: DateTime d/D/f/F/g/G/s/o/O; DateTimeOffset o/O; DateOnly d/D/o/O/r/R; TimeOnly t/T/o/O/r/R. Other patterns must satisfy full-date/type/offset rules. DateTime K permits an absent offset, but offset-bearing data is rejected. DateTimeOffset K still requires an actual explicit offset; zzz preserves both offset components.
- DateTime uses no assumed-zone flags; parsing checks Unspecified kind. DateTimeOffset checks the BCL explicit-zone marker, then parses without normalizing its offset. Native cells route through existing readers. Field trimming precedes exact parsing; no additional whitespace tolerance or general/invariant-writer fallback is added.
- WithCulture clones the culture and invokes generated ValidateCulture once during configuration to validate standard-pattern date/type/zone requirements. The new RecordMapper virtual has a no-op default for older/hand-written mappers. Ordinary query construction and per-row mapping do not add reflection, discovery, format-analysis objects, or format scans.
- Existing nullable-empty behavior and private location-aware conversion/write errors are retained. CSV quoting, limits, encoding, and mutation semantics remain shared. Formatting uses the reusable scalar buffer; culture/calendar/value-specific failures remain errors, without raw values or inner format exceptions.
- Dedicated boolean/null tokens, TimeSpan formats, and other roadmap features remain outside this change. Multiple input formats or separate read/write masks remain future objectives.

## Validation and review
- Human usage: docs/temporal-formats.md, mapped sample in samples/Models/Appointment.cs, and runnable TemporalFormatSample. Root GitHub/NuGet introduction and task guides link to it; no history is added to human guides.
- See history/validation-temporal-formats.md for local evidence. The existing three-platform CI runs the new tests and project/package/external-consumer Native AOT cases.
- Open the completed PR for human review after committing/pushing. The previous CI-fix merge authorization does not apply to this PR.
