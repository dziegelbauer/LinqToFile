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

## Open decision: empty fields and null values
- Proposal: an explicitly annotated converter receives empty input fields and null property values, and owns their interpretation. This permits custom null tokens and empty-field defaults without another option. Unannotated properties retain existing empty/null behavior.
- Await the user's answer before implementing this behavior.

## Next design/implementation work
- Define the annotation and a compile-time-checked parsing/formatting contract; settle null/empty and converter failure semantics.
- Add generator diagnostics for invalid converter/type/member combinations and preserve inheritance/ignore rules.
- Reuse generated read/write boundaries without runtime discovery, registration, or per-row converter objects.
- Test scalar overrides, domain types, nullable values, native XLSX kinds, culture, failures/privacy, write limits, allocations, cross-assembly use, and Native AOT.
- Keep dedicated built-in format/token annotations and unrelated roadmap features out of this first converter change unless separately approved.
