# XLSX execution implementation

## Authorization and status
- Stage 2 CI success was user-reported for 32ff4d2 and recorded in eaf4c18.
- The user authorized stage 3 with "commit and proceed". Stage 3 is implemented and locally validated; hosted checks remain pending. Do not call the complete feature validated on all CI platforms until the user confirms those results.
- No package publication or GitHub release is authorized. See ../validation-xlsx-stage3.md.

## Sources and resources
- XlsxSource mirrors CSV's IEnumerable plus async-foreach pattern, explicit AsAsyncEnumerable, and common list/array/dictionary materializers. XlsxAsync is an async-only factory entry point.
- StreamSource and TabularQuery retain claiming, ownership, deferred setup, concurrent path/factory enumeration, and cancellation contracts. XLSX does not introduce a second source-lifetime mechanism.
- XlsxInput creates one XlsxResources for spool and shared-string files. Non-seekable input copies from its current position with a pooled 64 KiB byte buffer, checks the workbook limit before writes, reserves temporary logical bytes, and rewinds only the owned spool.
- No-temp mode rejects non-seekable input before source bytes are read. Temporary directories are touched only when storage is needed, and are not created implicitly.

## Async boundaries and cancellation
- .NET 10 ZipArchive.CreateAsync and ZipArchiveEntry.OpenAsync exist and are used. The Entries getter still lazily calls synchronous central-directory loading in read mode. The seekable probe permits sync reads only in the directory range and rejects synchronous part reads.
- Archive seeks, Length/Position metadata, file creation, and XmlReader disposal remain synchronous. No Task.Run or blocking wait bridges sync and async traversal.
- Async XmlReader operations do not accept a token. XlsxXml.ReadAsync checks before/after each buffered node, and the counted part stream passes the enumeration token to underlying reads. Text processing checks at chunk boundaries.
- Spill files are opened with Asynchronous. Async writes are append-only during preprocessing; flush them asynchronously before random-read seeks to avoid synchronous buffered-write flushes. Async reads/writes use fixed byte buffers preserving raw UTF-16, with no per-item byte/string encoding allocation.
- Cleanup uses async package/resource disposal on async setup failures, cancellation, and completion. XML-reader closing itself is synchronous and owns only the part stream; the source lease owns the caller/factory stream.

## Maintaining parity
- Sync and async XML traversal live in paired partial files. They share native cell completion/conversion, text escape decoding, coordinate helpers, storage accounting, and schema/mapping/query behavior.
- Update both traversal paths when structural rules change. Tests cover representative native/rich/formula/error/temporal cases, both workbook variants, tiny reads, spooling and spill, cleanup, and cancellation at each preprocessing/read stage.
- Do not route async shared-string access through synchronous disk lookup or sync parser methods.
- Performance reports distinguish process-wide allocation, live-heap deltas, startup, and temporary logical lengths. These are not strict process-memory bounds.
