# Performance and memory

## Stream or materialize

Use `foreach` or `await foreach` to process records one at a time.
`Where`, `Select`, and `Take` can process records without retaining the complete result.
`ToList`, `ToArray`, and dictionary methods retain their selected records.
Operators such as `OrderBy` also retain records before returning a result.

Returned records own their values.
You can keep a record after enumeration advances or ends.
Dispose an enumerator that you use directly so the library can release its files and buffers.

Async queries permit asynchronous reads and cancellation.
They do not guarantee higher throughput than sync queries.
Measure your input, storage, and query before choosing a mode.

## CSV buffers and limits

The reader reuses record buffers within each enumeration.
Memory grows with the largest active record, not the total number of records read.
Returned strings and model objects still require their own storage.

`WithBufferSize(size)` sets the decoded input buffer in UTF-16 code units.
The default is 4096.
A larger buffer can reduce read frequency but increases memory per enumeration.
A small input buffer does not limit field or record length.

Use `WithMaxFieldLength(length)` and `WithMaxRecordLength(length)` to bound accepted content.
Both limits are unlimited by default.
They apply before trimming and include unmapped columns.
See the [CSV guide](csv-and-errors.md) for counting rules.

## XLSX startup

XLSX reading has setup work before the first record is returned:

1. Open the archive and read package/workbook metadata.
2. Load the shared-string table, when present.
3. Scan the selected worksheet for merged ranges.
4. Reopen the worksheet for record streaming.

Taking one record still requires this setup.
The reader does not materialize the complete worksheet or convert later data rows during the merge scan.
A non-seekable source adds a complete copy to a temporary file before the archive opens.

Async queries use async archive-entry, XML, and temporary-file I/O.
ZIP directory loading, seeks, and metadata access remain synchronous.
See the [XLSX guide](xlsx.md) for cancellation and ownership details.

## XLSX shared-string storage

`WithSharedStringMemoryBudget(bytes)` controls retained shared-string text and its in-memory index.
The default is 8 MiB.
If the table would exceed this budget, both content and a fixed-width index move to temporary files.
A zero budget sends all shared-string entries to disk.
There is no growing in-memory offset array after spill.

A larger budget can reduce disk access.
A smaller budget can reduce retained memory at the cost of temporary storage and lookup I/O.
Cached strings can be reused as immutable returned values.
Spilled strings require owned returned strings.

This budget is not a process-memory cap.
ZIP/workbook metadata, merged ranges, active row/text buffers, and caller-retained results use separate storage.
A large individual cell can grow the active text buffer.

## XLSX resource controls

| Option | Use |
|---|---|
| `WithMaxWorkbookBytes(bytes)` | Limit compressed input size from the source origin |
| `WithMaxDecompressedBytes(bytes)` | Limit processed part bytes, including repeated reads |
| `WithMaxCellTextLength(length)` | Limit resolved UTF-16 cell text before trimming |
| `WithMaxTemporaryBytes(bytes)` | Limit aggregate live temporary-file lengths |
| `WithTemporaryDirectory(path)` | Select an existing directory for temporary storage |
| `WithTemporaryFiles(false)` | Forbid temporary storage |

Size limits default to null, which means unlimited.
Zero is valid. Negative values are invalid.
Exceeding a configured limit throws without truncating the data.

Temporary storage includes the non-seekable workbook copy, shared-string content, and disk index.
The index uses 12 bytes per string, plus two content bytes per UTF-16 code unit.
The library releases owned temporary files on completion, cancellation, failure, or early exit.

With temporary files disabled, use a seekable source and a budget large enough for its shared strings.
See the [XLSX guide](xlsx.md) for complete counting and failure rules.
