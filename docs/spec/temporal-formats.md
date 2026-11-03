# Exact temporal format annotations

## Authorized objective
- Implement per-property exact date/time formats for DateTime, DateTimeOffset, DateOnly, and TimeOnly, including nullable forms.
- Apply shared mapping to CSV/XLSX reading and CSV output; preserve BCL-only runtime, Native AOT, no runtime reflection, and minimized allocations.
- Preserve existing conversions for unannotated properties. Define explicit culture/timezone behavior and compiler diagnostics for conflicting annotations.
- Update human usage guides and runnable examples; validate tests, allocations, package consumers, Native AOT, and hosted CI. Commit and push all work and open a PR for human review. Never merge it automatically.
- Stop for unresolved public behavior questions and ask one item at a time.

## First open decision
- Proposal: one TemporalFormat annotation specifies one .NET format string used both for exact text parsing and CSV formatting, for example [TemporalFormat("yyyy-MM-dd")]. No fallback to general parsing for annotated text fields.
- Multiple accepted input formats or distinct read/write formats can be considered later. This first proposal awaits user approval.

## Remaining contract work
- Settle native XLSX cell behavior, interaction with ColumnConverter/Ignore/inherited annotations, culture selection, and exact temporal timezone/missing-component rules.
- Keep empty nullable fields and existing error privacy/location rules unless the user approves a change. Use generated direct span-based calls rather than runtime annotation inspection.
- Validate annotation values and applicable property types. Do not claim all .NET format strings can be completely validated at compile time without establishing a reliable validation contract.
- Dedicated boolean/null tokens, TimeSpan formats, and other roadmap features are outside the authorized initial type scope.
