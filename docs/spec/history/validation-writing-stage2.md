# CSV writing stage 2 validation

Date: 2026-11-02. Branch: feature/row-writing-plan. Local host: macOS arm64, .NET 10.
Stage 2 is complete locally. Hosted Windows/Linux/macOS execution remains pending CI.
No commit, push, merge, or NuGet publication was performed.

## Implemented

- CsvFile<T>.Append(T), AppendRange(IEnumerable<T>) return long.
  AppendAsync(T, CancellationToken), AppendRangeAsync(IEnumerable<T>, CancellationToken), and
  AppendRangeAsync(IAsyncEnumerable<T>, CancellationToken) return ValueTask<long>.
  All operation tokens are optional. Null arguments throw; null range items produce private write errors.
- Existing destinations and valid headers are required. No append call creates a missing file.
  Exact write schema binding reuses the shared header rules and rejects extra columns without changing read behavior.
  Generated serialization follows file header order and reports actual bound header names.
- Full existing-file syntax/width/limit validation is the default, without old-value conversion.
  WithAppendValidation(false) retains header/schema/encoding/new-row checks and skips the old body.
  This option is independent of WithInPlaceWrites.
- Default append stages a byte-preserving complete result beside the destination, closes inputs and outputs,
  then uses File.Replace without backup or fallback. In-place append writes directly at physical EOF.
  Existing BOM bytes, quoting, whitespace, blank lines, and line endings are not rewritten.
- Header record endings are recognized separately from embedded newlines and skipped leading blank lines.
  Header EOF falls back to LF. NewLine is nullable internally so an explicit LF override differs from inference.
  Missing final terminators receive the selected separator only for a nonempty input.
  Single-column null/empty values keep the quoted-empty serialization rule.
- Empty ranges validate first, enumerate once, return zero without a temporary file or replacement,
  and preserve all bytes and the last-write timestamp.
- Existing process-local WriteGuard is shared by creation and append and released on all exits.
  File opens use FileMode.Open; default mode holds read access, direct mode holds exclusive read/write access.

## Encoding decision and probes

The user explicitly approved append support for UTF-8/16/32 and stateless single-byte encodings,
with rejection of other encodings before mutation. Reading and creation retain their existing support.
The selected encoding is checked after normal BOM selection. The runtime does not register encoding providers.
Runtime classification uses the UTF code pages or IsSingleByte; custom encodings must truthfully describe
their code page/single-byte behavior and satisfy the stateless contract.

The ignored BCL probe in artifacts/append-probes demonstrated a valid ISO-2022-JP file whose final ASCII
reset sequence was omitted. Existing decoding succeeds; appending a fresh encoder's bytes fails strict decoding.
This is why arbitrary encoder continuation is not promised.

Quick header reads feed the shared StreamReader incrementally, including the replayed BOM prefix.
This prevents decoder prefetch from validating body bytes after the header. CR header lookahead can ignore
a decoder error in the skipped body because CR already ends the header. Full validation retains that error.
Tests prove bounded header-only reads with an async-only one-byte source and a 100,000-byte body.
Tail checks compare encoded CR/LF bytes rather than starting a decoder in the middle of a UTF sequence.
UTF-8/16/32 BOM variants, no-BOM ASCII/Latin1, disabled BOM detection, BOM overrides, split supplementary
characters, invalid body bytes, and an EBCDIC single-byte encoding all pass.
Detected UTF encodings now preserve both configured fallback policies, so strict encoder behavior is not lost.

## Cancellation and failure boundaries

- Temporary mode honors cancellation through preparation, input enumeration/disposal, copying, and staging.
  It checks once more immediately before replacement. No cancellation is observed after replacement starts.
- Direct mode prepares the header/body validation, first input row, output buffers, and EOF position first.
  Immediately before its first destination write, it disables operation-token forwarding.
  An already-open async enumerator receives an internal forwarding token during preparation.
  That token is no longer cancelled by the operation token after the cutoff; later I/O receives None.
  This avoids changing enumerators or losing one-pass/empty-range behavior.
- Empty inputs remain cancellable, including when cancellation occurs during input disposal.
- Input enumeration and disposal finish before replacement. Their exceptions remain standard .NET exceptions.
  Serialization errors omit raw values and inner exceptions. Independent input cancellation still fails direct writes.
- Failure/cancellation cleanup deletes only owned staged files. Direct failure can leave the accepted partial prefix.
  Guards and handles are released on all tested failure paths.

## Filesystem behavior

A local File.Replace probe with an open read-only handle succeeds on this host, while the old handle retains
the original bytes. Tests expect sharing violations on Windows when a reader denies deletion; there is no fallback.
That Windows expectation still requires hosted execution.

The probe also showed that an ordinary replacement could change original Unix mode 0600 to staged mode 0644.
StagedFile therefore creates the temporary copy with at most the original rwx access bits, further limited by umask.
Tests inspect both the temporary file and committed result to verify that group/other read access is not added.
Ownership, ACLs, creation times, links/file identity, and other metadata follow filesystem replacement semantics;
no general preservation or power-loss durability guarantee is made. Human documentation states these limits.
Cross-process coordination and alias identity remain the caller's responsibility.

## Checks

All completed successfully on macOS arm64:
- dotnet build LinqToFile.sln -c Release -warnaserror: zero warnings/errors.
- dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore: 898 passed.
- Project-reference JIT and Native AOT publish/run.
- Pack LinqToFile.0.1.0-alpha.11.nupkg; version, installation, and current maintenance docs updated.
- Package-reference Native AOT sample with UseLocalPackage=true and the explicit package-validation NuGet config.
- eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64 with fresh external model/application projects and cache:
  zero runtime package dependencies, exact packaged README examples, CSV/XLSX reads, creation, all append
  overloads, both write modes, quick append, and scalar/temporal mappings under JIT and Native AOT.
- 117 append tests cover the sync/async input-mode matrix, exact schema errors and empty ranges,
  malformed old data versus quick mode, byte/BOM preservation, custom dialects, header endings/overrides,
  cancellation before/after mutation, disposal failures, privacy/limits/nulls, missing files, overlapping writes,
  bounded quick-header reads, native replacement sharing, and Unix temporary permissions.
- Numeric append compares 1,000 and 100,000 incrementally supplied rows after warmup with one reused model.
  Allocation growth stays within +/-4096 bytes. Async throughput/disk profiling remains stage 4.
- Human writing snippets are checked against the runnable cross-assembly sample.
- git diff --check.

The existing CI matrix runs the expanded tests and samples on Windows x64, Linux x64, and macOS arm64.
No hosted success is claimed for these unpushed changes.
Insertion is not implemented or exposed. Its byte-offset and in-place suffix movement work remains stage 3.
The final main README/NuGet introductory review remains an explicit stage 4 requirement.
Keep the unrelated docs/spec/release.md publication-verification change separate in a later commit.
