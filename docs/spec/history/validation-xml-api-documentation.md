# XML API documentation validation

## Scope
- Added XML summaries for externally visible runtime APIs, including enum values, attributes, overloads, exceptions, and generated-mapper support members. Added parameter, type-parameter, and return documentation where applicable.
- Enabled runtime XML documentation generation and compiler errors for missing or malformed documentation. Package checks require XML files for both targets, the expected assembly name, and nonempty member summaries.
- Added the standing requirement to AGENTS.md and docs/spec/documentation.md. Future API changes require the same documentation.

## Checks
- Release build: both net10.0 and net11.0, zero warnings and errors.
- Tests: 1,365 passed per target; no failures or skips.
- Package: 279 documented entries with nonempty summaries per target.
- External package validation: macOS arm64, .NET 10 and .NET 11; packaged README examples, cross-assembly mapping, sync/async operations, and Native AOT passed.
- Reviewed the complete diff against origin/main. It contains only documentation, documentation configuration, package checks, and these instructions/evidence.

- .NET 11 Native AOT emitted local Xcode/SDK debug-information diagnostics; publication and the native consumer completed successfully. These diagnostics do not come from the library XML documentation.

## Rider review
- Inspected all 20 changed C# files with warning severity, including suggestions and hints. RecordMapper.cs and TemporalMapping.cs returned no problems in individual checks; the batch tool omits clean files. No timeouts or incomplete batches were reported.
- Kept the established public LinqToFile namespace despite folder-name recommendations. Changing these namespaces would break public API compatibility.
- Kept existing target-typed construction, explicit guard clauses, backing fields, type declarations, patterns, and explicit attribute inheritance settings. These style recommendations do not improve documentation, compatibility, or runtime performance and would add unrelated code changes.
- Kept the existing short worksheet-name character search. A cached SearchValues instance would add static storage and initialization to a configuration path; no measured benefit justifies a runtime change in this task.
- Rider cannot inspect eng/Verify-Package.ps1 because it is outside the solution projects. The script was reviewed manually and executed by external package validation. Project files and Markdown are exempt from IDE inspection.
- Compiler validation checks XML syntax and public coverage. Tooltip content and package placement were inspected through the generated XML; no manual IDE hover session was performed.
