# CSV writing stage 3 validation

Date: 2026-11-02. Branch: feature/row-writing-plan. Local host: macOS arm64, .NET 10.
Stage 3 is complete locally. Hosted Windows/Linux/macOS execution remains pending CI.
No commit, push, merge, or NuGet publication was performed.

## Implemented contracts

- Insert(long position, T) and InsertRange(long position, IEnumerable<T>) return long.
  InsertAsync(long position, T, CancellationToken) and InsertRangeAsync overloads for IEnumerable<T> and
  IAsyncEnumerable<T> return ValueTask<long>. Tokens are optional. Input sequences are consumed once.
- Positions are zero-based logical data records; headers and skipped blank physical lines are excluded.
  Quoted multiline fields remain one record. Negative or greater-than-count positions throw.
  Beginning/middle insertion follows preceding skipped blank lines; position equal to count uses physical EOF,
  after trailing blank lines. Header-only insertion at zero works, including missing header terminators.
- Full existing CSV syntax, width, and limit validation always precedes mutation, independent of quick-append configuration.
  Existing values are not converted to model types. Existing-file headers bind exactly in file order.
- Both write modes preserve old bytes, BOM, whitespace, quoting, blank lines, mixed endings, and an unterminated suffix.
  New rows share generated serialization, encoding selection, dialect, culture, limits, line endings, null rules, and private diagnostics.
- Empty ranges validate the file and index, return zero, and preserve bytes and last-write timestamps without a staged file.
- Insertion uses the same existing-file encoding boundary as append: UTF-8/16/32 and stateless single-byte encodings.
  This follows the approved mutation boundary; read/create support remains unchanged.
  Stateful insertion is tested to fail before changes while reading/creation remain available.

## Byte positions and parser reuse

EncodedTextReader now exposes its existing BOM/encoding selection internally without changing normal StreamReader behavior.
PositionedTextReader decodes stateless encoded units with original byte-start offsets, rather than re-encoding old fields.
UTF-8 uses Rune unit boundaries; UTF-16 recognizes surrogate pairs and endianness; UTF-32 uses four-byte units;
single-byte encodings use their configured mapping. Decoder fallback policies remain active.

The existing CsvParser remains the only CSV syntax state machine. BufferedCharReader can consume the optional byte-offset
side buffer, and CsvParser captures the actual first character of each logical record after skipped blank lines.
Header BOM removal, multiline fields, CRLF boundaries, tiny buffers, supplementary characters, and malformed replacement
sequences are tested. Seeded malformed UTF-8/16/32 byte sequences decode identically to the BCL in the tested fixtures.

A decoder fallback can deliberately create multiple logical records from one encoded unit. A record start inside that
replacement has no independent byte boundary. Nonempty insertion there throws NotSupportedException before changes;
an empty range with a valid logical index remains a no-op. This edge is documented and tested. Insertion before the
whole encoded unit remains supported.

Full scans now retain whether decoded EOF has a record terminator. This also fixes validated append when a configured
fallback ignores trailing bytes: raw tail bytes alone must not introduce an extra blank record.
Quick append still uses its bounded raw tail check and does not promise to repair malformed encoded body data.

CsvAppendPreparation was renamed internally to CsvWritePreparation for shared formatting/schema/ending metadata.
Normal CSV/XLSX queries retain their previous source ownership and mapping behavior.

## Mutation algorithms

Temporary mode writes prefix + new rows + suffix into a sibling staged file, closes input and output resources,
then calls File.Replace with no fallback. Unix staged permissions retain the stage 2 restriction.
Failures and cancellation before commit leave the original unchanged and delete owned temporary files.

In-place mode:
1. Fully validate and locate the target, obtain the first input row, serialize it, and allocate movement buffers.
2. Append serialized new bytes at physical EOF, consuming the unknown-length input once.
3. Rotate [original suffix][new block] into [new block][original suffix] with three bounded reversals.
   Two 64 KiB movement arrays are allocated before the first write; no file spill or full-input collection is used.
   EOF insertion skips rotation. An EOF separator is added only when appending after an unterminated decoded record.

The prefix before the target is never moved. Suffix rotation uses only reads/writes within the extended file and does not
truncate it. The asynchronous path uses asynchronous stream reads/writes; seek/open/commit metadata operations remain synchronous.
It explicitly flushes asynchronously before seeks so buffered FileStream writes cannot cause hidden synchronous data flushes.
A strict buffered test stream rejects dirty-buffer length/position changes and all synchronous I/O.
The algorithm is linear in suffix plus new-block length, with repeated byte movement; broader throughput comparison belongs to stage 4.

The shared cancellation-forwarding gate is disabled immediately before the first in-place destination write.
Subsequent input enumeration, writes, rotation reads/writes, and flushing do not receive the operation token.
Independent input errors/cancellation and I/O failures can leave the new block at EOF or a partially moved suffix.
These accepted partial states are documented. Default mode remains cancellable through preparation and input disposal,
checks immediately before commit, and does not turn successful commit into cancellation.

## Fault and allocation checks

An internal IWriteFileSystem boundary permits deterministic stream/commit fault injection without global hooks or public APIs.
PhysicalWriteFileSystem uses the same native sharing/replacement rules and StagedFile permissions as the other mutation paths.

Fault tests execute successful baseline I/O sequences, then fail before and after each observed read, write, flush, close,
and commit for both sync/async execution and both modes. Async wrappers reject synchronous data I/O.
Before default commit the original remains unchanged; a reported failure injected after successful commit leaves the
committed result, with no rollback promise. Direct failures preserve the untouched prefix and may leave accepted partial data.
Every tested path releases handles/guards and removes owned temporary files; reopening and retrying on the same path succeeds.
Cancellation at the first write and before/after the commit action follows the approved cutoff rules.

Disk rotation probes compare exact bytes across odd/even and empty/one-byte suffixes using tiny odd-sized movement buffers.
Additional tests move a large Unicode suffix with 5,000 lazily generated input rows and confirm no temporary files appear.
Numeric insertion compares 1,000 with 100,000 incrementally supplied records after warmup, reusing a model.
Allocation growth remains within +/-4096 bytes. Larger async/throughput and retained-memory experiments remain stage 4.

## Validation

All completed on macOS arm64:
- Warning-free Release solution build.
- Full test suite: 1,024 passed.
- Project-reference JIT and Native AOT publish/run.
- Local package LinqToFile.0.1.0-alpha.12.nupkg.
- UseLocalPackage=true Native AOT publish/run.
- Clean external package consumer, fresh cache, separate model/application assemblies:
  runtime has no package dependencies; generator, license, metadata, and exact README snippets pass;
  CSV/XLSX reads plus creation, append, and every insertion overload execute under JIT and Native AOT.
- 103 insertion tests, 15 byte-position/rotation foundation cases, and 8 fault/cancellation cases
  (fault cases iterate all observed I/O points before/after).
- Three human writing examples match the executable sample.
- git diff --check.

The existing CI workflow automatically runs the expanded tests and samples on Windows x64, Linux x64, and macOS arm64.
No hosted validation is claimed for these unpushed changes.
Stage 4 remains: measurements/tuning, release checks, and the explicit user-requested main README/NuGet introductory review.
Keep the unrelated docs/spec/release.md publication-verification note separate when committing later.
