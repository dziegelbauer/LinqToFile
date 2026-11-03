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

## Open decision: custom converter conflicts
- Proposal: reject an effective mapping containing both TemporalFormat and ColumnConverter with a compiler diagnostic, including combinations inherited through overrides. A converter already owns parsing/formatting; silently prioritizing either annotation would hide a configuration error.
- Await the user's answer before implementing this behavior.

## Remaining contract work
- Settle interaction with ColumnConverter/Ignore/inherited annotations, culture selection, and exact temporal timezone/missing-component rules.
- Keep empty nullable fields and existing error privacy/location rules unless the user approves a change. Use generated direct span-based calls rather than runtime annotation inspection.
- Validate annotation values and applicable property types. Do not claim all .NET format strings can be completely validated at compile time without establishing a reliable validation contract.
- Dedicated boolean/null tokens, TimeSpan formats, and other roadmap features are outside the authorized initial type scope.
