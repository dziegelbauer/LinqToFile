# Writable CSV streams and factories

## Authorized direction
- The user agreed to tackle writable streams and stream factories next, enabling CSV output to HTTP responses, memory, and compression streams.
- Caller-supplied streams remain open by default, consistent with the reading API. The user approved this ownership default.
- Preserve .NET 10+, cross-platform compatibility, Native AOT, BCL-only runtime, no runtime reflection, minimized allocations, incremental input, and existing generated scalar/token/format serialization.
- Plan public behavior with the user, asking one item at a time. Suggestions below remain proposals until approved.
- Work on a feature branch, update current human guides/runnable examples, review Rider recommendations for every edited file, validate package/AOT/CI, and commit/push all changes before PR handoff. All merges require human approval; never merge or enable auto-merge.

## Confirmed initial stream scope
- Export a complete CSV document (header plus optional rows) to a writable stream, including non-seekable streams. Cover synchronous/asynchronous output and stream factories. Defer append/positional insertion into existing stream content, since those require a distinct seeking/readability/validation contract. The user approved this scope.

## Open decision: stream position
- Proposal: write at the supplied stream's current position without seeking, rewinding, or truncating it. Callers prepare/reset a seekable stream when they need replacement behavior. Bytes before the start position remain untouched; ordinary writes may overwrite existing bytes, and any tail beyond the output remains unless the caller removed it.
- Do not query Position/Length as a requirement for export, so non-seekable streams remain supported. Await the user's answer before fixing this contract.

## Remaining decisions
- Exact API names, repeated calls, factory ownership, flushing/BOM, cancellation/partial writes, and concurrency remain to be settled.

## Existing contracts to preserve
- Supplied read streams start at their current position, are single-use across derived configurations, and default to leaveOpen=true. Read factories return fresh library-owned streams.
- File-path writes retain their existing create/append/insert intent checks, mutation modes, guards, validation, and cancellation boundaries; do not silently apply path replacement guarantees to arbitrary streams.
- CSV output reuses shared generated mapping, custom dialect/encoding/culture/limits, LF default endings, converter/temporal/token annotations, null collision guards, and private structured write errors.

## IDE review
- Rider reported no findings for the AGENTS.md index edit. This topic is not included in an IDE project; its scope, confirmed/proposed separation, and index link were reviewed manually. Do not claim IDE analysis coverage for this topic.
