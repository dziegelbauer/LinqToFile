# .NET 11 support validation

## Approved scope

- Keep .NET 10 as the minimum. Ship net10.0 and net11.0 runtime assemblies in one package.
- Keep the generator on netstandard2.0. Use both frameworks for tests, samples, benchmarks, and package consumers.
- Validate both frameworks on Windows x64, Linux x64, and macOS arm64, including Native AOT.

## Local evidence

- Host: macOS arm64; SDKs 10.0.404 and 11.0.100. Build uses SDK 11.0.100.
- Release solution build with warnings as errors: zero warnings/errors.
- Tests: 1,336 pass per framework, zero failures/skips.
- Project-reference Native AOT publish and execution pass for both frameworks.
- Fresh external package consumers pass JIT and Native AOT for both frameworks, including README examples, cross-assembly generated mapping, CSV/XLSX read/write/mutation, annotations, and asynchronous sources.
- Package alpha.21 includes both runtime DLLs and one generator DLL, Apache-2.0 metadata, the exact repository README, and no runtime package dependencies.
- .NET 11 native linking reports SDK debug-map/module-cache warnings and an xcode-select diagnostic with Command Line Tools. Native compilation and execution succeed. No application-code change is indicated; native debug information can be incomplete on this host.
- Sandboxed MSBuild server connection failures stalled some pack/publish attempts. Approved unsandboxed retries completed; this is an environment limit rather than a project failure.

## IDE and manual review

- The only edited code/script file is eng/Verify-Package.ps1. Rider lint_files reports that it is not included in the loaded solution, so IDE inspection is unavailable. PowerShell parser validation and end-to-end execution pass for both allowed framework values. Manual review confirms that only validated framework names enter generated project XML.
- Project/build configuration and Markdown are exempt from IDE linting. Manually review the branch diff and check whitespace before handoff.
- CI expands to six jobs with framework-specific artifact names. Only Linux/net11.0 retains the publication package; publication still waits for every validation job and runs only after a main push. Human approval is required for merge.
- Hosted CI results will be recorded after the feature PR runs.
