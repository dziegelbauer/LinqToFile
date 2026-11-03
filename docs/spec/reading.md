# Reading and errors

## Confirmed
- Headers are required by default. Headerless CSV supports supplied column names or model ColumnIndex annotations; see headerless-csv.md.
- Throw when an error occurs.
- Encoding configurable through an option such as WithEncoding.
- Support incremental records and fully materialized collections.
- Every data row must have exactly as many fields as the header; throw for too few or too many, including surplus unmapped fields.
- Preserve field whitespace by default; provide an option to trim fields. Individual type parsers may accept whitespace according to their conversion rules.
- Enabled field trimming uses .NET whitespace rules after unescaping, for both quoted and unquoted values. Header trimming is a separate option.
- Accept file paths, caller-provided Stream instances, and factories that open fresh streams.
- Caller-provided streams remain open by default and permit only one enumeration.
- Paths and factories open a fresh stream per enumeration; the library disposes those streams.
- First release includes synchronous and asynchronous record streaming, with cancellation for async reads.
- Skip entirely blank physical lines outside quoted fields by default; provide an option to reject them. Lines with delimiters or quoted empty fields remain records.
- Default encoding is strict UTF-8: invalid byte sequences throw. BOM detection is enabled by default and configurable; recognized BOMs override the configured encoding when detection is enabled.
- When BOM detection is disabled, never switch encoding. Still remove the configured encoding's own matching BOM, including UTF encodings configured not to emit a preamble.
- Default delimiter is comma and default quote character is double quote; both are configurable.
- Comment lines are unsupported initially; characters such as # are ordinary field data.
- Delimiter and quote are distinct single UTF-16 characters; reject CR, LF, NUL, and surrogate characters for either option. Tab is permitted as a delimiter.
- Quoted fields support embedded newlines and doubled quote characters as escapes.
- Strict quoting: throw for quotes in unquoted fields, text (including whitespace) between a closing quote and the delimiter or record end, and unterminated quoted fields.
- Accept CRLF, LF, and CR record endings. Preserve original newline characters inside quoted fields.
- Empty files and files containing only skipped blank lines throw for a missing header. Valid header-only files produce an empty sequence.
- Query execution initially uses standard LINQ over IEnumerable<T>; defer a custom IQueryable provider. Fluent configuration supplies EF-style ergonomics.
- Configure options before switching to standard LINQ. Provide AsEnumerable() and AsAsyncEnumerable() as explicit boundaries; direct foreach and synchronous LINQ over configured synchronous sources also work.
- Async-only stream factories use a distinct entry point, CsvAsync<T>(factory). Paths and synchronous sources use Csv<T>() and support both synchronous and asynchronous enumeration.
- Error diagnostics include logical record number, physical line number, column index/header, and target member when applicable. Omit raw field values by default, including in surfaced conversion error details.
- Diagnostic physical lines, columns, and logical records are one-based. The header is logical record 1; skipped blank lines do not increment logical records. For multiline errors, report the record's starting line and the failure line when available.
- Parser, schema, and conversion failures use a library-specific exception family with structured location/member properties. Cancellation and I/O failures retain standard .NET exception types.
- Validate configuration immediately. Report unsupported model shapes through compiler diagnostics. Defer file, header, and record errors until enumeration reaches the relevant input.
- Reuse parser buffers internally while returning records with owned values. Borrowed row views are outside first-release scope.
- Parser buffer size is configurable. Grow buffers only as required by active fields/records; parser memory scales with the largest active record rather than the entire file.
- Buffer defaults, pooling strategy, and other internal performance tuning are implementation decisions guided by benchmarks, subject to public configuration and ownership contracts.
- Optional maximum field and record sizes throw when exceeded; both default to unlimited.
- Phase 2 APIs use nullable int lengths: null removes a limit, zero permits zero-length content, and negative values are rejected immediately. Limits apply to headers and unmapped columns as well as mapped values.
- Field limits count decoded UTF-16 code units in the unescaped field before trimming. Record limits count decoded source UTF-16 code units, including delimiters, quotes, and embedded newlines, excluding the record-ending newline.
- WithBufferSize sets the decoded-character input buffer length (default 4096), independently of field/record length. Size must be positive. Internal encoding buffers remain separate.
- Validate schema/header mappings before yielding any data records, including for header-only files. Missing mapped columns and duplicate headers still throw when there are no data rows.
- Cancellation throws OperationCanceledException. Dispose library-owned streams on cancellation, completion, failure, and early termination; caller-owned streams remain open by default.
- Check cancellation between records as well as during async reads, including while processing already-buffered input.
- Support synchronous Func<Stream> factories and async factories accepting CancellationToken and returning ValueTask<Stream>.
- Concurrent enumerations over paths and factories are independent; each factory invocation must supply a fresh stream.
- A caller-provided Stream permits one enumeration total across all derived configurations, including synchronous and asynchronous use.
- Read caller-provided streams from their current position without seeking or rewinding; non-seekable streams are supported.
- Atomically claim a caller-provided stream's single enumeration on the first MoveNext/MoveNextAsync attempt. Failure or cancellation still consumes that allowance. Creating a query or enumerator alone does not claim it.
- Synchronous enumeration of an async-only stream factory throws a clear exception rather than blocking on asynchronous work.
- Caller-provided streams expose an explicit leaveOpen ownership option, defaulting to true. Setting it to false transfers disposal responsibility to the library.
- Initial async materialization API includes ToListAsync, ToArrayAsync, and ToDictionaryAsync, alongside async streaming. Use .NET 10 BCL async LINQ operators for filtering and projection.
- ToDictionaryAsync supports a key selector, optional value selector (otherwise the mapped record is the value), optional key comparer, and cancellation token. Duplicate keys throw, following standard LINQ behavior.
- Library ToDictionaryAsync duplicate-key errors preserve ArgumentException but omit the key. Its shared builder uses TryAdd rather than the BCL duplicate-key message. User callbacks and explicit BCL operations retain their own errors.

## Future
- Configurable per-column error behavior through annotations.
- Opt-in configuration to include raw field values in error diagnostics, similar to EF sensitive-data diagnostics.

## Implemented foundation
- Deferred execution.
- No whole-file buffering for ordinary streaming. Unlimited field/record size defaults mean parser memory is not capped independently of record size.

## Unresolved
- Broader performance and platform checks remain in phase 5.
- Per-column recovery and opt-in raw diagnostic values remain future goals.
