# Boolean and null tokens

## Authorized objective
- Implement the selected roadmap item: per-property boolean tokens and explicit null tokens for shared CSV/XLSX reading and CSV writing.
- Preserve .NET 10+, cross-platform behavior, BCL-only runtime, Native AOT, no runtime reflection, and minimized allocations. Use generated span comparisons and existing reusable output storage.
- Unannotated properties retain existing conversions, empty-field behavior, and error privacy/location rules.
- Update human usage documentation and runnable examples, validate generator/runtime/package/AOT/hosted CI, commit and push all changes, and open a PR for human review. Never merge it or enable auto-merge.
- Ask unresolved public behavior questions one item at a time. Suggestions below are proposals until approved.

## First open decision: boolean token scope
- Proposal: [BooleanTokens("yes", "no")] specifies exactly one true token and one false token, used for both text parsing and CSV output. The annotation replaces built-in text tokens rather than silently also accepting true/false. Boolean properties without it keep current true/false behavior.
- Multiple accepted aliases or separate read/write tokens can be considered later. Await the user's answer before fixing this contract.

## Remaining contract work
- Define case comparison with typed options, trimming, empty/null distinctions, applicable property types, native XLSX behavior, canonical null output, collision validation, and interaction with custom converters/temporal formats/inheritance/Ignore.
- Token strings are user-provided data, not magic strings for fixed options. Configuration choices must use enums or other typed values.
- Preserve existing public setter/init and write getter requirements. Keep format parsing and CSV escaping/mutation separate from shared mapping.
