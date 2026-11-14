# Constructor mapping validation

## Scope

- Implements the user-approved ordinary-class constructor contract in mapping.md.
- Adds TabularConstructor and LTF009, property-based constructor binding, shared conversion locals, and direct construction.
- Retains parameterless mapping and write getters. Records and primary constructors remain deferred.
- Includes human reference/XML documentation, an example drift check, and a separately compiled immutable sample model.

## Local validation

Host: macOS arm64, .NET 10 and .NET 11.

- Release solution build: zero compiler warnings and errors. Rider solution build also succeeds.
- Complete tests: 1,397 pass for each target framework.
- Constructor tests cover selection, parameter order, case-insensitive ambiguity, type/nullability mismatch, ignored/static/inaccessible properties, inheritance, required members, unsupported parameters, optional/zero parameters, and primary-constructor rejection.
- Runtime tests cover sync/async CSV/XLSX, native workbook values, token/temporal/custom/enum/nullable conversions, headerless indexes, conversion locations/privacy, application exceptions, and write round trips.
- The 100,000-row allocation comparison permits no per-row allocation increase over initializer mapping (4 KiB fixed tolerance, minimum of three warmed measurements).
- Project-reference .NET 10 Native AOT publish/run succeeds with TreatWarningsAsErrors.
- Package alpha.21 passes metadata/XML/license/README checks and fresh external two-project JIT and Native AOT consumption for both net10.0 and net11.0. Both consumers execute the new CSV/XLSX constructor sample.
- The .NET 11 native linker emits host toolchain messages: xcodebuild is unavailable with CommandLineTools selected, a duplicate debug-map object, and absent Swift/Foundation debug module caches. Publish exits successfully and the native consumer passes. No library compiler or AOT compatibility diagnostics were reported.
- Reviewed the complete branch diff from origin/main and checked whitespace. Hosted platform CI remains separate from this local evidence.

## Rider inspection review

Inspected all changed code files with warning severity (including suggestions/hints):

- src/LinqToFile.Generator/RecordGenerator.cs
- src/LinqToFile/Mapping/Attributes.cs
- tests/LinqToFile.Tests/ConstructorGeneratorTests.cs
- tests/LinqToFile.Tests/ConstructorMappingTests.cs
- tests/LinqToFile.Tests/DocumentationTests.cs
- samples/Models/ImmutableProduct.cs
- samples/NativeAot/ConstructorMappingSample.cs
- samples/NativeAot/Program.cs

Program.cs was checked individually because the batch omitted it. Rechecked constructor test/sample files individually or in a smaller batch after a successful Rider build. The final generator diagnostic wording was also re-inspected.

Accepted UTF-8 literal suggestions in fixed test inputs. Compiler checks also corrected the writer test calls to WriteAsync. Kept explicit array creation for writer overload clarity and existing explicit-type/style conventions. Retained the public LinqToFile namespace for attribute API compatibility and explicit attribute inheritance metadata for clarity. Partial declarations are required by generation. Synchronous Assert.Throws callbacks finish before their captured streams are disposed. Pre-existing regex/array/style suggestions in documentation tests do not justify unrelated changes.

Inspection limits: Rider continues to omit generated ITabularRecord implementations from some semantic inspections, including an existing AllocationRow model. It reports unresolved generic constraints and secondary Assert.Throws overload ambiguity in constructor tests/sample code, despite successful Rider/compiler builds, Roslyn generator compilation tests, and JIT/AOT executions. These files are not reported as inspection-clean. No timeouts or incomplete results remained in the final inspections. eng/Verify-Package.ps1 is not included in a Rider project and could not be inspected; both complete package-validation executions pass.
