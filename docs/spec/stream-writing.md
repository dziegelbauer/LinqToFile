# Writable CSV streams and factories

## Authorized direction
- The user agreed to tackle writable streams and stream factories next, enabling CSV output to HTTP responses, memory, and compression streams.
- Caller-supplied streams remain open by default, consistent with the reading API. The user approved this ownership default.
- Preserve .NET 10+, cross-platform compatibility, Native AOT, BCL-only runtime, no runtime reflection, minimized allocations, incremental input, and existing generated scalar/token/format serialization.
- Plan public behavior with the user, asking one item at a time. Suggestions below remain proposals until approved.
- Work on a feature branch, update current human guides/runnable examples, review Rider recommendations for every edited file, validate package/AOT/CI, and commit/push all changes before PR handoff. All merges require human approval; never merge or enable auto-merge.

## First open decision: initial stream scope
- Proposal: export a complete CSV document (header plus optional rows) to a writable stream, including non-seekable streams. Cover synchronous/asynchronous output and stream factories. Defer append/positional insertion into existing stream content, since those require a distinct seeking/readability/validation contract.
- Await the user's answer before fixing this scope. Exact API names, start-position/truncation semantics, repeated calls, factory ownership, flushing/BOM, cancellation/partial writes, and concurrency remain to be settled.

## Existing contracts to preserve
- Supplied read streams start at their current position, are single-use across derived configurations, and default to leaveOpen=true. Read factories return fresh library-owned streams.
- File-path writes retain their existing create/append/insert intent checks, mutation modes, guards, validation, and cancellation boundaries; do not silently apply path replacement guarantees to arbitrary streams.
- CSV output reuses shared generated mapping, custom dialect/encoding/culture/limits, LF default endings, converter/temporal/token annotations, null collision guards, and private structured write errors.

## IDE review
- Rider reported no findings for the AGENTS.md index edit. This topic is not included in an IDE project; its scope, confirmed/proposed separation, and index link were reviewed manually. Do not claim IDE analysis coverage for this topic.
