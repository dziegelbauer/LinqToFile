# Sources and query execution

## Confirmed requirements
- One runtime package, with build-time generator tooling.
- Plan beyond CSV: keep format parsing separate from source ownership, schema binding, queries, and generated mapping.
- Support file paths, supplied streams, synchronous stream factories, and async factories.
- Paths and factories open independent library-owned streams per enumeration.
- Supplied streams start at their current position and default to leaveOpen=true.
- All options derived from one supplied-stream source share one atomic enumeration allowance.
- Claim on the first MoveNext/MoveNextAsync, including failed or cancelled attempts. Creating or disposing an unused enumerator does not claim.
- Async-only factories use CsvAsync<T>; synchronous enumeration throws without invoking them.
- Cancellation is checked during async I/O and between records. Dispose owned sources on completion, failure, cancellation, or early exit.
- ToListAsync, ToArrayAsync, and ToDictionaryAsync are required. Dictionary supports key/value selectors, comparer, cancellation, and duplicate-key errors.
- Library-created errors omit raw values by default.

## Implemented in phase 4
- Sources/: shared stream acquisition and sync/async disposal leases.
- Reading/: shared text decoding, buffering, options, reader contracts, and schema binding.
- Queries/: format-independent sync/async enumeration and materialization.
- Formats/Csv/: CSV options, parser, reader adapter, exception, and fluent source.
- Infrastructure/: format-neutral TabularRow and generated RecordMapper<T> contract.
- Mapping/: model attributes and static mapping interface.
- Keep public namespaces stable; folders separate responsibilities inside one runtime project.
- CsvSource<T> implements IEnumerable<T>, exposes the async-foreach pattern, and returns IAsyncEnumerable<T> through AsAsyncEnumerable(). This preserves direct synchronous LINQ without ambiguous sync/async extension overloads.
- Reuse BCL async filtering/projection/list/array methods. A shared dictionary builder uses TryAdd and a generic ArgumentException message so duplicate keys do not disclose values.
- User callbacks and explicitly called BCL operators retain their own exception behavior.
- The internal reader boundary is tested with a synthetic non-CSV byte format. It is not a public provider API.
- XLSX stage 1 extends TabularRow with optional native cell kinds and worksheet/part context, while preserving the CSV text-only path. Shared TextOptions now isolates encoding/BOM/buffer options from common TabularOptions. XLSX reuses StreamSource, schema binding, and TabularQuery for synchronous paths/seekable streams; factories and async sources remain later XLSX stages.
