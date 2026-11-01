# XLSX storage implementation notes

## Storage and accounting
- SharedStringStore initially retains strings in a List with capacity accounted before growth. Budget includes conservative aligned UTF-16 string storage, list storage, and its reference array. Control/parser objects and active row buffers are outside that budget; this is not a total-process cap.
- When memory would exceed budget, content and index both move to generated DeleteOnClose files. The index uses 12 bytes per entry (64-bit offset and 32-bit UTF-16 length). Character payload uses native UTF-16 bytes within the same process, preserving raw code units. No disk format is exposed publicly.
- After spill there is no growing offset array or string cache in RAM. Lookup uses fixed buffers. Cached in-memory strings may be returned directly when trimming does not alter them; values remain immutable and owned after enumeration ends.
- Shared strings are eagerly preprocessed when the relationship exists. MaxCellTextLength applies to every item then, including unused entries. This can report structural/limit failures before selected rows are reached.
- XlsxResources counts all consumed decompressed part bytes, including repeated worksheet reads. MaxTemporaryBytes counts live logical data/index lengths, not filesystem allocation or cumulative traffic. Workbook limit is the remaining archive length from the supplied origin.
- Native boolean strings count their canonical true/false lengths. Text-run escape decoding happens before resolved-cell limit checks, with a conservative raw-length guard to bound expansion work for finite cell limits.

## Conversion boundaries
- Native numeric cells opt into Excel serial/duration behavior through NumericDateSystem set by the XLSX reader. Its default None keeps other format adapters from inheriting Excel semantics.
- ExactDecimal canonicalizes significant digits and exponent, avoids giant powers for obviously out-of-range/tiny values, and uses exact arithmetic for tick rounding. Numeric-to-integer parsing keeps the fast BCL span path and an exact fallback for unusual exponents.
- NativeTemporal validates ISO syntax independently from a target's representable rounded result. String targets preserve valid stored ISO text even when a temporal target would overflow after rounding.
- Formula text is skipped without evaluation or storage. Cached cell types select conversion rules. Missing caches fail; explicit empty cached string values are valid and formula presence keeps the row nonblank.
- Optional presentation markup can be skipped; unsupported must-understand or required data markup still fails. This is feature-subset parsing, not whole-package XSD validation.

## Validation and next work
- Independent fixture source/license/provenance is in tests/LinqToFile.Tests/Fixtures/Xlsx/README.md. These files do not add a runtime dependency.
- Stage 2 is complete. Stage 3 adds async factories, non-seekable spooling, and profiling; see xlsx-execution.md and history/validation-xlsx-stage3.md.
