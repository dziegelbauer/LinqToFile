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
- The exact claim boundary and failure/cancellation behavior remain separate details.

## Confirmed public API shape
- Separate write-only entry points TabularFile.CsvWriter<T>(stream, leaveOpen=true) and CsvWriter<T>(Func<Stream>), with CsvWriterAsync<T>(Func<CancellationToken, ValueTask<Stream>>) for async-only factories. Return an immutable configurable CsvWriter<T> with Write/WriteAsync operations that export a complete document. Keep existing Csv<T>(stream/factory) reading APIs unchanged. The user approved these names and shape.
- Example: await TabularFile.CsvWriter<Person>(stream).WithDelimiter(';').WriteAsync(rows). Input overloads, counts, and headers-only export will follow existing file-writing conventions unless a separate behavior question requires confirmation.

## Open decision: factory ownership
- Proposal: factory-returned streams are library-owned and disposed after each export, including failure or cancellation. Async operations use asynchronous disposal. Caller-supplied streams retain the approved leaveOpen=true default, with leaveOpen=false explicitly transferring disposal responsibility to the operation.
- Await the user's answer before fixing factory ownership. Cancellation and partial-write behavior remain separate decisions.

## Remaining decisions
- Flushing/BOM, cancellation/partial writes, and concurrency remain to be settled.

## Existing contracts to preserve
- Supplied read streams start at their current position, are single-use across derived configurations, and default to leaveOpen=true. Read factories return fresh library-owned streams.
- File-path writes retain their existing create/append/insert intent checks, mutation modes, guards, validation, and cancellation boundaries; do not silently apply path replacement guarantees to arbitrary streams.
- CSV output reuses shared generated mapping, custom dialect/encoding/culture/limits, LF default endings, converter/temporal/token annotations, null collision guards, and private structured write errors.

## IDE review
- Markdown files are exempt under the updated standing rule. This topic and its index/guidance edits receive manual review and diff checks; no IDE lint is required for them.
