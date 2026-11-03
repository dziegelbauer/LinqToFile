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

## Confirmed empty fields
- Retain existing empty-field rules alongside the explicit null token. Empty strings remain empty strings; empty nullable value fields remain null. Null strings can use the explicit token, so null and empty string remain distinct. Quoted/unquoted empties remain equivalent. The user approved this behavior.
- The explicit token is an additional null representation rather than a change to existing absence semantics.

## Confirmed null comparison
- Default NullToken matching to StringComparison.Ordinal (case-sensitive), so [NullToken("NULL")] does not consume ordinary text such as "null". Permit Comparison = StringComparison.OrdinalIgnoreCase as an explicit opt-in. Only these two enum values are supported, independent of culture. The user approved this behavior.
- CSV null output retains the exact spelling supplied in the annotation.

## Confirmed null-token property types
- Support NullToken on nullable built-in value types (including enums) and explicitly nullable string properties (string?). Reject non-nullable properties with a compiler diagnostic, since they cannot represent the annotated null value. The user approved this behavior. Custom converter interaction remains a separate decision.

## Confirmed token whitespace
- Require boolean and null tokens to be nonempty and have no leading/trailing .NET whitespace; invalid annotation values produce compiler diagnostics. Internal whitespace remains literal token content. The user approved this behavior.
- Input matching uses the field after configured WithFieldTrimming; no extra token-specific trimming occurs. This avoids padded tokens becoming unreadable when trimming is enabled.

## Confirmed native XLSX cells
- Apply tokens to CSV fields and XLSX text cells only. Native XLSX booleans keep their typed true/false values, independent of BooleanTokens. Native numeric/date/boolean cells cannot become null merely because their storage text matches a null token; existing target-type conversion boundaries remain unchanged. Empty/missing cells retain the already approved empty-field rules. The user approved this behavior.

## Confirmed custom converter conflicts
- Reject effective combinations of ColumnConverter with either BooleanTokens or NullToken as compiler errors, including inherited combinations. A custom converter already owns token parsing, formatting, empty fields, and null values; do not silently prioritize or layer annotations around it. The user approved this behavior.

## Confirmed non-null output collisions
- Throw the existing private, location-aware TabularWriteException if a non-null value's formatted text would match its configured null token when read with the same options. Apply the configured null comparison and field trimming. This covers strings and all other annotated built-in types; quoting cannot disambiguate decoded token content. The user approved this behavior.
- Example: [NullToken("NULL")] string? cannot write a non-null literal "NULL"; [NullToken("0")] int? cannot write zero. Null values still write the configured token.

## Open decision: annotation composition
- Proposal: allow NullToken with BooleanTokens on bool?, and with TemporalFormat on supported nullable temporal types. Check the text null token before ordinary value conversion; a match yields null. On writes, null emits its token and bypasses ordinary formatting; non-null values use their boolean/temporal/built-in formatter and the approved output-collision check.
- Native XLSX cells and empty fields retain the already confirmed rules. Await the user's answer before fixing this composition contract.

## Remaining contract work
- Define case comparison with typed options, trimming, empty/null distinctions, applicable property types, native XLSX behavior, canonical null output, collision validation, and interaction with custom converters/temporal formats/inheritance/Ignore.
- Token strings are user-provided data, not magic strings for fixed options. Configuration choices must use enums or other typed values.
- Preserve existing public setter/init and write getter requirements. Keep format parsing and CSV escaping/mutation separate from shared mapping.
