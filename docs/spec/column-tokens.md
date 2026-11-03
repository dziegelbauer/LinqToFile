# Boolean and null tokens

## Authorized objective
- Implement the selected roadmap item: per-property boolean tokens and explicit null tokens for shared CSV/XLSX reading and CSV writing.
- Preserve .NET 10+, cross-platform behavior, BCL-only runtime, Native AOT, no runtime reflection, and minimized allocations. Use generated span comparisons and existing reusable output storage.
- Unannotated properties retain existing conversions, empty-field behavior, and error privacy/location rules.
- Update human usage documentation and runnable examples, validate generator/runtime/package/AOT/hosted CI, commit and push all changes, and open a PR for human review. Never merge it or enable auto-merge.
- Ask unresolved public behavior questions one item at a time. Suggestions below are proposals until approved.

## Confirmed boolean token scope
- [BooleanTokens("yes", "no")] specifies exactly one true token and one false token, used for both text parsing and CSV output. The annotation replaces built-in text tokens rather than silently also accepting true/false. Boolean properties without it keep current true/false behavior. The user approved this behavior.
- Multiple accepted aliases or separate read/write tokens remain future objectives.

## Confirmed boolean comparison
- Default to StringComparison.OrdinalIgnoreCase, consistent with built-in boolean parsing. Permit an explicit Comparison = StringComparison.Ordinal for case-sensitive tokens. Only these two enum values are supported, keeping comparisons independent of culture. The user approved this behavior.
- CSV output uses the literal token spelling from the annotation; comparison changes input matching only.

## Confirmed null token scope
- [NullToken("NULL")] specifies one token used both to recognize null on text input and to serialize null to CSV. Do not automatically recognize other null spellings. The user approved this behavior. Multiple aliases or separate read/write null tokens remain future work.

## Open decision: empty fields
- Proposal: retain existing empty-field rules alongside the explicit null token. Empty strings remain empty strings; empty nullable value fields remain null. Null strings can use the explicit token, so null and empty string remain distinct. Quoted/unquoted empties remain equivalent.
- This treats the explicit token as an additional null representation rather than changing existing absence semantics. Await the user's answer before fixing this contract.

## Remaining contract work
- Define case comparison with typed options, trimming, empty/null distinctions, applicable property types, native XLSX behavior, canonical null output, collision validation, and interaction with custom converters/temporal formats/inheritance/Ignore.
- Token strings are user-provided data, not magic strings for fixed options. Configuration choices must use enums or other typed values.
- Preserve existing public setter/init and write getter requirements. Keep format parsing and CSV escaping/mutation separate from shared mapping.
