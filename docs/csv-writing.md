# Write CSV files

Use a path source to create a file.
The parent directory must exist.
The destination must not exist.
Creation never overwrites a file.

The model uses the same attributes as a read query.
Each mapped property must have a getter accessible to the generated mapper.
Public getters work across assemblies.
A setter-only model can still be read, but its write calls throw before any file changes.
This check also applies to headers-only creation.

[Custom converters](custom-converters.md) control field formatting for annotated properties.
They receive null values and the configured culture.
CSV quoting, encoding, separators, and output limits still apply to their results.

## Create with initial rows

The following example uses the `Person` model from the sample.

```csharp
var file = TabularFile.Csv<Person>(path)
    .WithDelimiter(';')
    .WithQuote('\'')
    .WithNewLine(LineEnding.Lf);
long written = await file.CreateAsync(new[]
{
    new Person { Id = 1, Name = "Ada; 'Lovelace'" },
    new Person { Id = 2, Name = "Grace" }
});
```

Read the result through the same file configuration.
Creation returns the number of data rows written.
The header is not included in this count.

## Append rows

Append requires an existing file with a valid header.
It does not create a missing file or supply a header for an empty file.
Use creation first when you need a new file.

```csharp
long appended = await file.AppendAsync(new Person { Id = 3, Name = "Katherine" });
appended += file.AppendRange(new[]
{
    new Person { Id = 4, Name = "Dorothy" },
    new Person { Id = 5, Name = "Mary" }
});
```

Each append returns the number of new data rows.
On `CsvFile<T>`, `Append` executes a file write.
Use `AsEnumerable().Append(item)` when you want the standard LINQ sequence operation.
An empty range returns zero and leaves all file bytes unchanged.
It still checks the file, headers, and selected validation rules.
New rows follow the file's header order.
The headers must match the mapped model columns exactly under the selected comparison.
Missing and extra columns throw.
Reading still ignores extra unmapped columns.

## Insert rows

Insertion requires an existing file with valid headers and CSV syntax.
The position is a `long` index into logical data records.
The header and skipped blank lines do not count.
A quoted multiline record counts as one row.

| Position | Meaning |
|---|---|
| Zero | Insert before the first data record |
| Existing data-row count | Insert at physical EOF, after trailing blank lines |
| Negative or greater than the count | Throw `ArgumentOutOfRangeException` |

```csharp
long inserted = await file.InsertAsync(1, new Person { Id = 10, Name = "Rosalind" });
inserted += file.InsertRange(2, new[]
{
    new Person { Id = 11, Name = "Hedy" },
    new Person { Id = 12, Name = "Jean" }
});
```

When inserting before a record, new rows follow any blank lines before that record.
All existing bytes keep their original order and content.
New rows use the file's header order and selected line ending.
An empty range checks the whole file and position, returns zero, and changes no bytes.

## Methods

| Method | Input | Result |
|---|---|---|
| `Create()` | No initial rows | `long` |
| `Create(records)` | `IEnumerable<T>` | `long` |
| `CreateAsync(token)` | No initial rows | `ValueTask<long>` |
| `CreateAsync(records, token)` | `IEnumerable<T>` or `IAsyncEnumerable<T>` | `ValueTask<long>` |
| `Append(record)` | One `T` | `long` |
| `AppendRange(records)` | `IEnumerable<T>` | `long` |
| `AppendAsync(record, token)` | One `T` | `ValueTask<long>` |
| `AppendRangeAsync(records, token)` | `IEnumerable<T>` or `IAsyncEnumerable<T>` | `ValueTask<long>` |
| `Insert(position, record)` | A `long` position and one `T` | `long` |
| `InsertRange(position, records)` | A `long` position and `IEnumerable<T>` | `long` |
| `InsertAsync(position, record, token)` | A `long` position and one `T` | `ValueTask<long>` |
| `InsertRangeAsync(position, records, token)` | A `long` position and either sequence type | `ValueTask<long>` |

The cancellation token is optional.
Headers-only creation and an empty initial sequence return zero.
Each sequence is read once, one row at a time.
The library does not collect all input rows in memory.
Async methods use async file writes.
They do not run synchronous creation on a background thread.

Path calls return `CsvFile<T>`.
All shared CSV options preserve that type.
Stream and factory calls return read-only `CsvSource<T>` objects.
Those objects have no write methods.
Generic read helpers can accept `CsvSource<T>`.

## File content

New headers use ordinal alphabetical order by mapped column name.
`Column` supplies the name when present.
Otherwise, the property name supplies the name.
`Ignore` excludes a property.
Header comparison and header trimming must permit the generated header to bind to the model.

The default encoding is strict UTF-8 without a byte-order mark (BOM).
`WithEncoding` selects another encoding.
Creation writes that encoding's configured preamble.
`WithBomDetection` controls reading; it does not control preamble output.

Newly created files use LF on every platform.
Use `WithNewLine(LineEnding.CrLf)`, `LineEnding.Lf`, or `LineEnding.Cr` to select an ending.
Undefined enum values throw immediately.
For append, the header record supplies the default line ending.
An unterminated header uses LF.
The option overrides the ending for new content only.
Append preserves every existing byte, including blank lines and mixed line endings.
When necessary, it adds a separator after the last existing record.

Writing uses the configured delimiter and quote character.
It quotes fields that contain either character, CR, or LF.
It doubles embedded quote characters.
It preserves field whitespace, including when field trimming is enabled for reading.
Null properties and empty strings produce empty fields.
A single-column empty value is quoted so that reading does not skip the row.
A null input row throws.

Field and record limits also apply to output, including headers.
Field limits count unescaped UTF-16 units.
Record limits include delimiters, quotes, and embedded newlines.
They exclude the record terminator.

## Value formats

Numbers use the configured culture.
Floating-point values retain round-trip precision.
NaN and infinity throw.
Booleans use lowercase `true` and `false`.
GUIDs use the hyphenated format.

Enums use declared names.
An exact named value takes priority over a flags combination.
Aliases use the first name in ordinal alphabetical order.
Flags combinations use names in the same order.
Undefined values and unnamed zero values throw.

Temporal values use invariant text.
`DateTime` and `DateTimeOffset` use the BCL `O` format.
`DateOnly` uses `yyyy-MM-dd`.
`TimeOnly` uses `HH:mm:ss.fffffff`.
`TimeSpan` uses the BCL `c` format.
The reader recognizes these formats before culture-based parsing.
This rule also applies to non-Gregorian cultures.
`DateTime` must have `DateTimeKind.Unspecified`.
Local and UTC kinds throw.
`DateTimeOffset` keeps its explicit offset.

## Creation modes and cancellation

The default mode prepares the complete file in a temporary file beside the destination.
It publishes the result only after serialization and writes succeed.
Publication does not overwrite a destination created by another writer.
Before publication, an error or cancellation removes the owned temporary file.
Successful publication reports success.
There is no crash or power-loss durability guarantee.

Use `WithInPlaceWrites()` to create the destination directly.
This mode avoids the temporary file.
An input error or I/O failure can leave a partial file.
The operation token is checked during preparation.
After destination creation starts, the operation stops observing that token.
It also stops passing the token to async input enumeration.
Independent input cancellation or an input error can still fail the operation.

## Append validation and modes

By default, append checks all existing CSV syntax and row widths.
It does not convert existing values to model types.
Invalid existing syntax throws before any destination changes, in either write mode.

Use `WithAppendValidation(false)` for quick append.
Quick append checks the header, encoding, and new rows.
It skips validation of existing data.
It does not repair malformed existing content.
Output limits still apply to new rows.

Validation and write mode are independent options:

| Configuration | Existing-data work | Temporary file |
|---|---|---|
| Default | Full validation and byte copy | Complete result |
| `WithAppendValidation(false)` | Header check and byte copy | Complete result |
| `WithInPlaceWrites()` | Full validation | None |
| Both options | Header and file-tail checks | None |

Default append prepares the result beside the destination.
It replaces the destination only after input enumeration, serialization, and writes succeed.
Before replacement, errors and cancellation leave the original unchanged.
Owned temporary files are removed.
The filesystem must support same-directory replacement.
A replacement failure throws; it does not select in-place writing as a fallback.
Replacement follows filesystem metadata rules and can change file identity.
The library limits Unix temporary-file permissions to the original access bits and current process mask.
It does not promise to preserve ownership, access-control lists, or all other metadata.
Open readers can prevent replacement on some platforms.

Use `WithInPlaceWrites()` to append directly.
This mode holds an exclusive file handle during preparation and writing.
Input queries that open the same path can fail.
An input error or I/O failure can leave partial changes.
There is no rollback or crash-durability guarantee.

Async append honors the operation token during preparation and initial input enumeration.
Immediately before the first in-place destination write, it stops observing that token.
Later cancellation of that token does not cancel the already-open async input.
Independent input cancellation and input failures can still throw.
Default append honors cancellation until immediately before replacement.
Successful replacement reports success.
Empty ranges remain cancellable because no destination write occurs.

## Insertion modes and cancellation

Insertion always validates all existing syntax, row widths, and limits before changing the destination.
It does not convert old values to model types.
`WithAppendValidation(false)` does not disable insertion validation.

Default insertion prepares the prefix, new rows, and suffix in a temporary file beside the destination.
It replaces the destination after preparation succeeds.
The replacement, metadata, cleanup, and cancellation rules are the same as for default append.

`WithInPlaceWrites()` inserts without a temporary file or a collection of all input rows.
It consumes the input once and moves the existing suffix with fixed-size buffers.
This mode needs space for the new rows in the destination.
It can perform substantial I/O when the suffix is large.
After the first destination write, operation-token cancellation is ignored through input enumeration and suffix movement.
Input errors, independent cancellation, I/O failures, or process termination can leave incomplete changes.
New rows can remain at EOF or the suffix can be partly moved.
Use the default mode when the original must remain unchanged on preparation failure.

## Encodings for existing files

Append and insertion support UTF-8, UTF-16, UTF-32, and stateless single-byte encodings.
Other encodings throw `NotSupportedException` before file changes.
This restriction does not change encoding support for reading or creation.
The library does not register an encoding provider.
Custom encodings must report their encoding characteristics correctly and satisfy this stateless contract.

BOM detection and `WithEncoding` select the encoding as they do for reading.
Append keeps the original BOM bytes and never adds another BOM.
Insertion also preserves the existing BOM and adds none.
When BOM detection selects a different UTF encoding, the configured fallback policies still apply.
If a decoder replacement creates a record start inside text from the same encoded unit, that start has no separate byte position.
A nonempty insertion at that position throws `NotSupportedException` before file changes.

## Concurrent writes

Only one library write can be active for a normalized path in a process.
An overlapping write throws immediately.
Coordinate separate processes yourself.
Path normalization does not establish file identity across links or aliases.

## Errors

Serialization failures throw `TabularWriteException`.
`RecordNumber` is the one-based input row number.
Zero identifies model preparation.
`ColumnIndex` is one-based in output header order when available.
`ColumnHeader` and `TargetMember` identify the mapped property when available.
Physical line properties are zero because the error describes input objects.
Serialization errors omit raw values and inner exceptions.

Generated header and schema errors use `TabularSchemaException`.
Invalid existing CSV syntax or row widths use `CsvFormatException`.
I/O errors, input enumeration errors, and cancellation keep their standard exception types.
The library does not replace an exception supplied by an input sequence.
