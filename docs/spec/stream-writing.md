# Writable CSV streams and factories

## Authorized direction
- The user agreed to tackle writable streams and stream factories next, enabling CSV output to HTTP responses, memory, and compression streams.
- Caller-supplied streams remain open by default, consistent with the reading API. The user approved this ownership default.
- Preserve .NET 10+, cross-platform compatibility, Native AOT, BCL-only runtime, no runtime reflection, minimized allocations, incremental input, and existing generated scalar/token/format serialization.
- Plan public behavior with the user, asking one item at a time. Suggestions below remain proposals until approved.
- Work on a feature branch, update current human guides/runnable examples, review Rider recommendations for edited code files (Markdown/non-code files are exempt), validate package/AOT/CI, and commit/push all changes before PR handoff. All merges require human approval; never merge or enable auto-merge.

## Confirmed initial stream scope
- Export a complete CSV document (header plus optional rows) to a writable stream, including non-seekable streams. Cover synchronous/asynchronous output and stream factories. Defer append/positional insertion into existing stream content, since those require a distinct seeking/readability/validation contract. The user approved this scope.

## Confirmed stream position
- Write at the supplied stream's current position without seeking, rewinding, or truncating it. Callers prepare/reset a seekable stream when they need replacement behavior. Bytes before the start position remain untouched; ordinary writes may overwrite existing bytes, and any tail beyond the output remains unless the caller removed it. The user approved this behavior.
- Do not query Position/Length as a requirement for export, so non-seekable streams remain supported.

## Confirmed repeated exports
- A caller-supplied stream destination permits one export total across that destination and its derived configurations, consistent with supplied-stream read sources. Factory destinations permit repeated exports by opening a fresh stream per call. The user approved this behavior.
- The claim boundary and failure/cancellation behavior are specified below.

## Confirmed public API shape
- Separate write-only entry points TabularFile.CsvWriter<T>(stream, leaveOpen=true) and CsvWriter<T>(Func<Stream>), with CsvWriterAsync<T>(Func<CancellationToken, ValueTask<Stream>>) for async-only factories. Return an immutable configurable CsvWriter<T> with Write/WriteAsync operations that export a complete document. Keep existing Csv<T>(stream/factory) reading APIs unchanged. The user approved these names and shape.
- Example: await TabularFile.CsvWriter<Person>(stream).WithDelimiter(';').WriteAsync(rows). Input overloads, counts, and headers-only export will follow existing file-writing conventions unless a separate behavior question requires confirmation.

## Confirmed factory ownership
- Factory-returned streams are library-owned and disposed after each export, including failure or cancellation. Async operations use asynchronous disposal. Caller-supplied streams retain the approved leaveOpen=true default, with leaveOpen=false explicitly transferring disposal responsibility to the operation. The user approved this ownership contract.

## Confirmed cancellation and partial output
- Stream exports honor the operation token throughout async input enumeration and output I/O, including after writing starts. Cancellation or other failures may leave partial output; no rollback, atomic publication, or complete-document guarantee applies. This supports responsive HTTP/network exports without adding temporary storage. The user approved this behavior.
- Existing file-path staged/in-place cancellation rules remain unchanged.

## Confirmed supplied-stream claim boundary
- After validating the call's arguments, atomically consume the supplied destination's one-export allowance when the export attempt starts, before observing operation cancellation or writing bytes. Subsequent failure/cancellation still consumes that attempt, including cancellation before any output. Derived configurations share the same claim. The user approved this boundary.
- Creating/configuring a writer does not consume its allowance; invalid arguments do not constitute an export attempt.

## Confirmed successful flushing
- Successful Write/WriteAsync finalizes the export's encoder/library buffers and flushes the destination stream before returning. Async export uses asynchronous writes/flush and honors its token through the final flush. Owned stream disposal also completes before success is reported; supplied streams with leaveOpen=true remain open. The user approved this completion behavior.
- Flushing does not promise crash durability or finalize a caller-owned compression wrapper; its caller still controls wrapper disposal. Failure/cancellation cleanup and pending-buffer behavior remain implementation details to validate against the approved partial-output contract.

## Confirmed encoding preamble
- Write the configured encoding's preamble once at the start of each exported CSV document, even at a nonzero underlying stream position. Default strict UTF-8 has no BOM. Do not inspect or alter existing bytes, and do not make preamble behavior depend on stream seekability. The user approved this behavior.
- This is a new document at the current position, not an append operation.

## Confirmed factory concurrency
- Allow concurrent exports through a factory-backed writer, with each invocation receiving its own newly opened stream, encoder, and output buffers. The factory must supply an independent fresh stream per call and support the concurrent invocation pattern chosen by the caller. The user approved this behavior.
- Supplied-stream destinations retain their shared atomic single-use allowance, so overlapping attempts fail immediately.

## Existing contracts to preserve
- Supplied read streams start at their current position, are single-use across derived configurations, and default to leaveOpen=true. Read factories return fresh library-owned streams.
- File-path writes retain their existing create/append/insert intent checks, mutation modes, guards, validation, and cancellation boundaries; do not silently apply path replacement guarantees to arbitrary streams.
- CSV output reuses shared generated mapping, custom dialect/encoding/culture/limits, LF default endings, converter/temporal/token annotations, null collision guards, and private structured write errors.

## Implementation
- CsvWriter<T> holds immutable CsvOptions and a shared StreamDestination identity. CsvStreamExport<T> reuses CsvOutput<T> and EncodedOutput; no reflection, new runtime dependencies, or whole-sequence buffering is introduced.
- Validate model/header serialization before opening a factory. Supplied streams are already claimed and receive ownership cleanup if model validation fails.
- Each factory operation allocates its own serialization buffers and encoder; configurations do not cache mutable export state. Skip zero-byte underlying writes when an encoder produces no bytes.
- On failure, do not finalize/flush pending CSV encoder data. Still dispose owned streams, asynchronously for WriteAsync. Retain the primary export exception if stream cleanup also fails, except OutOfMemoryException; surface disposal failures after otherwise successful output.
- Header-only, single-row, IEnumerable<T>, and asynchronous IAsyncEnumerable<T> overloads return data-row counts. Supplied leaveOpen=false ownership applies to attempted exports, not to constructing/configuring an unused writer.
- Runnable samples and package checks cover supplied/factory/async-only/compression output; the stream guide is checked against the sample. See [validation](history/validation-stream-writing.md) for evidence and IDE review.

## IDE review
- Markdown files are exempt under the updated standing rule. This topic and its index/guidance edits receive manual review and diff checks; no IDE lint is required for them.
