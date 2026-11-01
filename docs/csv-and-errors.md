# CSV rules and errors

## Current CSV rules

The file must contain a header record.
Missing mapped headers cause errors.
Extra headers are permitted.
Empty headers and whitespace-only headers cause errors.
Duplicate headers cause errors, including duplicates in unmapped columns.
The selected header comparer controls duplicate detection.

Each data record must have the same field count as the header.
A header-only file returns no records after header validation.
An empty file causes a missing-header error.

The current delimiter is a comma.
The current quote character is a double quote.
A quoted field can contain commas and newlines.
Two adjacent quotes inside a quoted field represent one quote.
Quotes inside unquoted fields cause errors.
Text after a closing quote causes an error, including whitespace.

The parser accepts CRLF, LF, and CR record endings.
Quoted fields keep their original newline characters.
The parser skips an entirely blank line outside a quoted field.
A line with spaces, delimiters, or quoted empty values remains a record.
A terminating newline is not necessary for the final record.

The default encoding is strict UTF-8.
BOM detection is enabled.
A recognized byte order mark (BOM) overrides the selected encoding.
The detected encoding keeps the selected decoder fallback policy.
`WithEncoding` selects another BCL encoding and its decoder fallback policy.

## Error behavior

Invalid options throw when the option method runs.
Unsupported models cause compiler errors.
File, header, and data errors occur during enumeration.

| Exception | Cause |
|---|---|
| `CsvFormatException` | Invalid quotes or a record width mismatch |
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
Errors do not include raw field values or parser exception details.
I/O and decoder failures keep their standard .NET exception types.

## Later phases

Configurable delimiters, quoting, BOM detection, trimming, and size limits are planned.
Configurable blank-line handling and additional conversions are also planned.
Async sources and cancellation are not available yet.
Per-column recovery and optional raw values in errors are future goals.
