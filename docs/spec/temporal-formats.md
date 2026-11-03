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

## Open decision: intentionally omitted output components
- Proposal: selecting a format explicitly permits omission of time components or fractional seconds from output. For example, TimeOnly with HH:mm writes 14:30 even when the value also has seconds. Do not reject a value merely because its omitted components are nonzero; document that reading the result cannot recover those components. Existing date-completeness and timezone/offset requirements still apply.
- Await the user's answer before implementing this output behavior.

## Remaining contract work
- Settle interaction with Ignore/inherited annotations and exact format validation/missing-component rules.
- Keep empty nullable fields and existing error privacy/location rules unless the user approves a change. Use generated direct span-based calls rather than runtime annotation inspection.
- Validate annotation values and applicable property types. Do not claim all .NET format strings can be completely validated at compile time without establishing a reliable validation contract.
- Dedicated boolean/null tokens, TimeSpan formats, and other roadmap features are outside the authorized initial type scope.
