# CSV rules and errors

## File and header rules

The file must contain a header record.
Missing mapped headers cause errors.
Extra headers are permitted.
Empty headers and whitespace-only headers cause errors.
Duplicate headers cause errors, including duplicates in unmapped columns.
The selected header comparer controls duplicate detection.

Each data record must have the same field count as the header.
A header-only file returns no records after header validation.
An empty file causes a missing-header error.
Header trimming occurs before validation, duplicate detection, and column matching.
Annotation names keep their literal whitespace.

## Options and defaults

| Method | Default | Effect |
|---|---|---|
| `WithDelimiter(character)` | Comma | Selects a single-character field delimiter |
| `WithQuote(character)` | Double quote | Selects a single-character quote |
| `WithBomDetection(enabled)` | True | Selects encoding from a recognized BOM |
| `WithFieldTrimming(enabled)` | False | Removes surrounding whitespace from field values |
| `WithHeaderTrimming(enabled)` | False | Removes surrounding whitespace from header names |
| `WithBlankLineSkipping(enabled)` | True | Skips entirely blank physical lines |
| `WithBufferSize(size)` | 4096 | Sets the decoded input buffer size in UTF-16 code units |
| `WithMaxFieldLength(length)` | Null | Limits unescaped field length before trimming |
| `WithMaxRecordLength(length)` | Null | Limits decoded source record length |

The delimiter and quote must differ.
Neither character can be CR, LF, NUL, or a UTF-16 surrogate.
Tab is a permitted delimiter.
A buffer size must be positive.
A length limit must be non-negative or null.
Null removes a length limit.
Zero permits only zero-length content within that limit.

All options return new immutable sources.
An invalid option causes an immediate exception.
The boolean methods select true when the caller omits the argument.

## Quoting and whitespace

A quoted field can contain delimiters and newlines.
Two adjacent quote characters inside a quoted field represent one quote.
Quotes inside unquoted fields cause errors.
Text after a closing quote causes an error, including whitespace.
Whitespace removal does not relax these quote rules.

The parser accepts CRLF, LF, and CR record endings.
Quoted fields keep their original newline characters.
A terminating newline is not necessary for the final record.

The parser skips entirely blank physical lines by default.
With blank-line skipping disabled, such a line causes a format error.
A line with spaces, delimiters, or quoted empty values remains a record.
Characters such as `#` are field data, not comment markers.

Field trimming uses .NET whitespace rules after unescaping.
It applies to quoted and unquoted fields.
It also applies before nullable-value conversion.
Header trimming is a separate option.

## Length limits

Limits count UTF-16 code units.
A supplementary Unicode character counts as two code units.
Field limits count unescaped characters before trimming.
An escaped quote counts as one field character.

Record limits count decoded source characters.
They include delimiters, opening and closing quotes, escaped quote pairs, and embedded newlines.
They exclude the terminating record newline.
An embedded CRLF counts as two code units.

Both limits apply to headers and all data columns, including unmapped columns.
The parser checks a limit before it stores content beyond that limit.
A limit violation causes `CsvFormatException`.

The default limits are unlimited.
Memory grows with the largest active record rather than the total file size.
The input buffer and the record buffer have separate responsibilities.
A small input buffer does not limit field length.

## Encoding and BOM behavior

The default encoding is strict UTF-8.
A recognized byte order mark (BOM) selects its encoding when detection is enabled.
The recognized BOMs identify UTF-8, UTF-16 LE/BE, and UTF-32 LE/BE.
The detected encoding keeps the selected decoder fallback policy.

With detection disabled, the reader uses only the configured encoding.
The reader still removes that encoding's own matching BOM.
This rule also applies to UTF encodings configured not to emit a preamble.
A foreign BOM never causes an encoding switch in this mode.

`WithEncoding` selects another BCL encoding and its decoder fallback policy.
The source copies the supplied encoding.
Later changes to that encoding do not change the source.

## Configured example

The sample file uses semicolons, single quotes, and padded headers.
The [executable sample](../samples/NativeAot/Program.cs) checks this query:

```csharp
var configured = TabularFile.Csv<Person>(path)
    .WithDelimiter(';')
    .WithQuote('\'')
    .WithHeaderTrimming()
    .WithFieldTrimming()
    .WithBufferSize(3)
    .WithMaxFieldLength(64)
    .WithMaxRecordLength(128)
    .WithBomDetection(false)
    .WithBlankLineSkipping(false)
    .Single();
```

## Error behavior

Invalid options throw when the option method runs.
Unsupported models cause compiler errors.
File, header, and data errors occur during enumeration.

| Exception | Cause |
|---|---|
| `CsvFormatException` | Invalid quotes, blank lines, length limits, or a record width mismatch |
| `TabularSchemaException` | A missing header or invalid column mapping |
| `TabularConversionException` | A field cannot convert to its target type |

These exceptions derive from `TabularFileException`.
They give location properties when the information is available:

- `RecordNumber`
- `LineNumber` for the start of the record
- `FailureLineNumber`
- `ColumnIndex`
- `ColumnHeader`
- `TargetMember`

Record numbers, line numbers, and column indexes start at 1.
The header is record 1.
Skipped blank lines increase line numbers but do not increase record numbers.
A column index is a field ordinal, not a character position.
A width error identifies the first missing or extra column.
Errors do not include raw field values or parser exception details.
I/O and decoder failures keep their standard .NET exception types.

## Related guides

The [conversion guide](conversions.md) gives the supported types and culture rules.
The [source guide](sources-and-async.md) gives stream, async, and cancellation behavior.
