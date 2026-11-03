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
- Support NullToken on nullable built-in value types (including enums) and explicitly nullable string properties (string?). Reject non-nullable properties with a compiler diagnostic, since they cannot represent the annotated null value. The user approved this behavior. Custom converter conflicts are defined below.

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

## Confirmed annotation composition
- Allow NullToken with BooleanTokens on bool?, and with TemporalFormat on supported nullable temporal types. Check the text null token before ordinary value conversion; a match yields null. On writes, null emits its token and bypasses ordinary formatting; non-null values use their boolean/temporal/built-in formatter and the approved output-collision check. The user approved this behavior.
- Native XLSX cells and empty fields retain the already confirmed rules.

## Implementation choices
- BooleanTokensAttribute(string trueToken, string falseToken) exposes TrueToken/FalseToken and a Comparison enum property. NullTokenAttribute(string token) exposes Token and Comparison. These strings are user-provided data; fixed comparison choices use StringComparison enum values, never magic strings.
- LTF007 diagnoses invalid property types, null/empty/padded tokens, unsupported comparisons, and true/false tokens that compare equal. Each override inherits the nearest annotation of its kind; a derived annotation replaces that kind. Column renames remain supported. Effective Ignore/ColumnConverter conflicts are LTF004. Existing setter/init/getter diagnostics remain unchanged.
- BooleanTokens supports bool and bool?. NullToken requires a supported nullable value type or explicitly annotated string?; oblivious/non-nullable strings are rejected. Existing nullable-empty checks stay after the text null-token check.
- The generator emits literal tokens and typed comparison values through ReadBooleanTokens/IsNullToken and direct SetText/typed formatting. Metadata objects exist only during compilation. No runtime attribute discovery, registration, or per-field token objects are used.
- Native non-text cells use existing conversions. Non-null output is formatted once, then compared to its null token before escaping. TabularValue receives the CSV trimming option; RejectNullToken uses spans and throws without data, then CsvOutput supplies private write locations. A null write bypasses value formatting/collision checks.
- Null tokens can reserve ordinary values, including enum names, dates, numbers, or boolean spellings. Null input matching has priority. Do not reject such reservations statically: enforce the approved write-collision rule with the null comparison alone. True/false pair ambiguity is rejected at compile time.
- Keep format parsing and CSV escaping/mutation separate from shared mapping. Source options, file intent checks, cancellation, staged/in-place write behavior, headers, resource limits, and encoding rules remain unchanged.
- Human docs include docs/column-tokens.md and runnable InventoryItem/TokenSample examples. Per the user's added instruction, refresh the shared GitHub/NuGet README with friendly, benefit-led copy; retain installation/examples/links and move detailed restrictions to guides. Package verification still runs the exact five introductory C# blocks.

## Validation and review
- Tests cover diagnostics, inheritance, nullable composition, comparison/trimming, native cell boundaries, reserved-value output guards, private locations, CSV escaping/limits, and zero additional allocations in warmed token operations.
- Project/package and fresh external-consumer samples include cross-assembly boolean/null/date annotations, async input, append/insertion, collision protection, and native XLSX preservation. See history/validation-column-tokens.md for evidence.
- Commit and push all changes, run hosted checks, and open a PR for human review. Do not merge this PR or enable auto-merge.
