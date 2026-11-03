# Writable CSV stream validation

## Coverage
- Adds complete-document synchronous/asynchronous exports to supplied writable streams and fresh stream factories. Existing path mutation behavior stays unchanged.
- Tests cover current-position writes without truncation/seek/read, non-seekable output, UTF-8/16/32 preambles, immutable options, dialect/culture/tokens, limits and private diagnostics, single-use/concurrent attempts, repeatable concurrent factories, sync/async-only factories, ownership and awaited disposal, cancellation, partial multi-buffer output, encoding/factory/input/I/O errors, cleanup precedence, and compression wrappers.
- Allocation checks warm the synchronous/asynchronous paths and compare the minimum of three 1,000-row versus 100,000-row exports to a fixed-memory sink. The allowed difference is 4 KiB; generated scalar conversion and output buffers do not allocate per row.
- Human guides and the friendly shared GitHub/NuGet introduction now link the stream export guide. Its two C# examples match the runnable NativeAot sample. The package verifier copies and executes that sample with separately compiled model types.

## Rider review
- Reviewed warnings, suggestions, and hints for TabularFile.cs, StreamDestination.cs, CsvWriter.cs, CsvStreamExport.cs, EncodedOutput.cs, StreamWritingSample.cs, NativeAot/Program.cs, StreamWriterTests.cs, StreamWriterBoundaryTests.cs, ExportProbeStream.cs, and DocumentationTests.cs. Rider's solution rebuild completed successfully with no build problems.
- Removed unused System.Text and System.Globalization imports and re-inspected both files. No code errors were reported.
- Retained public LinqToFile namespace for CsvWriter, matching the existing public API despite the format folder. Kept established private-field naming and explicit numeric types/target-typed construction rather than unrelated style churn.
- Retained synchronous calls/disposal where samples and tests deliberately exercise sync behavior or in-memory cleanup. Callback captures run and are awaited within the enclosing resource lifetime; no callback escapes beyond disposal. Test assertion-only parameters and an explicit null factory cast document the tested overload. Exact float assertions use exactly representable constants. GC.SuppressFinalize follows the Stream.DisposeAsync pattern even though these sealed test streams have no finalizer. Collection-expression, local-function placement, guard-to-return, and existing regex-style suggestions were evaluated and left for consistency/readability.
- Rider cannot analyze eng/Verify-Package.ps1 because it is outside the solution projects. Reviewed its changes manually; execution is part of package validation. Markdown and other non-code files are exempt from IDE linting and receive manual review/diff checks.

## Results
- Release solution build passed with zero warnings/errors; all 1,214 tests passed with none skipped. JIT samples passed.
- Project-reference Native AOT publish/run passed on macOS arm64 with warnings as errors.
- Packed alpha.17, verified the exact shared README, package assets, and zero runtime package dependencies, then ran the fresh external consumer under JIT and Native AOT. The separate model assembly and stream examples passed.
- Repository sample restored from the local package into a fresh streams-package-cache and passed Native AOT publish/run. Combined validation command exited zero; log: ignored artifacts/streams-final.log.
- Hosted Windows/Linux/macOS checks are run on the pull request; their live results are linked in its check list.
