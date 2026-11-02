# CSV writing stage 1 validation

Date: 2026-11-02. Branch: feature/row-writing-plan. Local host: macOS arm64, .NET 10.
Stage 1 is complete locally. Hosted Windows/Linux/macOS execution remains pending CI.
No commit, push, merge, or NuGet publication was performed for this stage.

## Implemented

- CsvFile<T> derives from CsvSource<T>; covariant fluent options retain creation capability.
  Stream/factory entry points remain read-only. Generic helpers returning CsvSource<T> still compile.
- Create()/Create(IEnumerable<T>) return long. CreateAsync accepts no rows, IEnumerable<T>, or IAsyncEnumerable<T>,
  with optional CancellationToken, returning ValueTask<long>.
- Default creation stages a sibling temporary file, closes it, then publishes with File.Move(overwrite: false).
  In-place creation uses FileMode.CreateNew. Neither mode overwrites an existing destination.
- Incremental one-pass input, reusable scalar/record/encoding buffers, persistent encoder, no runtime dependencies/reflection.
  Generated WriteField accesses getters directly; the shared TabularValue formatter has no CSV dependency.
- Public and same-assembly accessible getters work. Setter-only/inaccessible-getter models retain reads.
  Their writes, including headers-only creation, fail before opening a destination or temporary file.
- Ordinal alphabetical mapped headers, shared annotations/dialect/culture/encoding, typed line endings (default LF),
  whitespace preservation, null/empty rules, quoting, output limits, counts, and private write errors.
- Invariant temporal writer formats are recognized before culture-based reading without relaxing DateTime kind
  or DateTimeOffset explicit-offset requirements.
- Cross-assembly creation samples execute from both project and packaged generator references.
  Human usage lives in docs/csv-writing.md, with an automated snippet drift check.
  Unimplemented append/insert and other formats remain on the roadmap.

## Engineering choices

- Generated formatting uses per-property dispatch, preserving deterministic CSV order independently of model declaration order.
  RecordMapper's new virtual members default to read-only behavior, preserving existing mapper read implementations.
- Numeric integers/decimal use G; Half/float/double use R with finite checks.
  Booleans use lowercase text; GUID uses D; DateTime/DateTimeOffset/DateOnly/TimeOnly use invariant O; TimeSpan uses c.
- Exact enum constants take precedence. Alias choice and flags token order use ordinal alphabetical names.
  Flags accumulate declared subsets of the original value, permitting overlapping named masks.
  Generated constants use numeric casts, preserving support for obsolete enum names and signed/high-bit values.
  Uncovered bits and unnamed zero fail. No runtime enum discovery or formatting occurs.
- Write RecordNumber is the one-based input data-row number; zero means model preparation.
  ColumnIndex uses one-based output order when available; it is omitted during unsupported-model preparation.
  Physical line properties are zero for write errors. Getter/format/encoder details are not retained as inner exceptions.
  Header/schema failures use TabularSchemaException; I/O, enumeration, and cancellation errors retain their own types.
- Guard keys use Path.GetFullPath, ordinal comparison except ordinal-ignore-case on Windows.
  Entries are removed on every operation exit. Link/alias identity and cross-process coordination are not promised.
- Undefined LineEnding values fail immediately. New-file encoding emits its configured preamble.
  BufferSize remains a reading option; writer buffers are internal implementation choices.
- The CSV path entry point's new return type is a prototype binary API change.
  Source-level generic read helper compatibility is tested; do not claim old compiled consumers are drop-in compatible.
  Package base version is now 0.1.0-alpha.10; normal main CI will add its unique run suffix after merge.

## Narrow probes

An ignored BCL console probe under artifacts/write-probes exercised:
- Non-overwriting File.Move preserves an existing destination and its staged input; sibling File.Replace succeeds
  on this host. Creation's publication-race test runs in the normal platform matrix. Existing-file replacement
  permissions/metadata/open-reader behavior still needs stage 2 platform checks.
- UTF-8, UTF-16 LE/BE, and UTF-32 decoder consumption with one-byte chunks preserves CRLF and supplementary Unicode.
  Decoder state is required; do not infer future byte-preserving insertion offsets by independently re-encoding fields.
  Full offset scanning and stateful/custom existing-file encoding constraints remain stage 2/3 work.
  Creation uses one persistent encoder and does not narrow the agreed WithEncoding API.
- Ordinary parsing of invariant ISO DateOnly text fails to retain the date under th-TH, ar-SA, and fa-IR.
  Exact invariant parsing succeeds. Normal tests cover these cultures, minimum dates, maximum times, full ticks,
  TimeSpan.MinValue, and explicit offsets. An exact DateTimeOffset parse can infer a missing offset;
  canonical recognition therefore requires the explicit offset suffix before parsing.
- Three segment reversals rotate an appended block before an existing suffix without whole-input materialization.
  This was an in-memory algorithm identity probe, not a disk implementation or a fault-safety claim.
  Bounded disk movement and fault injection remain stage 3 acceptance work.

## Checks

All completed successfully on this host:
- dotnet build LinqToFile.sln -c Release -warnaserror: zero warnings/errors.
- dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore: 781 passed.
- Project-reference JIT sample and Native AOT publish/run (osx-arm64).
- Pack local feed: LinqToFile.0.1.0-alpha.10.nupkg.
- Package-reference sample Native AOT publish/run with UseLocalPackage=true.
- eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64:
  verifies package metadata, license, two-DLL payload, zero runtime dependencies, exact README examples;
  builds separate model/application projects outside the repository with a fresh package cache;
  runs CSV/XLSX reads and CSV creation under JIT and Native AOT.
- New tests cover both mutation modes, sync/async inputs, cancellation boundaries and independent input cancellation,
  one-pass enumeration, guard overlap/release, publication races, existing files, failure cleanup, null rows,
  getter privacy, strict encoding errors, UTF BOM variants, large Unicode fields, line endings, limits,
  nullable/scalar matrix, culture snapshots, aliases/overlapping flags/high bits, compile-time capabilities,
  and setter-only read compatibility.
- Numeric creation allocation compares 1,000 and 100,000 rows after warmup, reusing the same model.
  The measured growth stays within +/-4096 bytes. This excludes whole-sequence retention and per-row formatting allocations.
  Broader async/throughput/disk measurements remain stage 4.
- git diff --check.

The existing CI workflow automatically exercises the expanded tests, project/package samples, and clean consumer
on Windows x64, Linux x64, and macOS arm64. No hosted result is claimed for this unpushed work.
Keep the unrelated docs/spec/release.md publication-verification change separate in any later commit.
