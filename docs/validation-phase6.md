# Phase 6 validation

## Status

Release preparation passes local checks on macOS arm64.
The expanded clean-consumer check is included in the three-platform CI workflow.
The user confirmed successful CI after commit `d8c7030`.
Phase 6 is complete. Hosted results are recorded from that confirmation; run logs were not independently retrieved.
No public package or GitHub release has been created by this work.

## Package

The local version is `0.1.0-alpha.6`.
The package includes these files:

- `lib/net10.0/LinqToFile.dll`
- `analyzers/dotnet/cs/LinqToFile.Generator.dll`
- `README.md`
- `LICENSE`

The manifest has no runtime package dependencies.
It declares the Apache-2.0 license and the GitHub repository URL.
It includes project information, search tags, and prerelease notes.
The packaged README uses absolute documentation links.
The license text matches the repository license.

## Consumer validation

The clean-consumer script creates a fresh workspace outside the repository.
It uses a fresh NuGet cache and maps `LinqToFile` to the local feed.
Two projects verify generated mapping across assembly boundaries.
The application consumes the runtime through its model-project dependency.
It does not receive repository build properties or source-project references.

The check passes under JIT and Native AOT on macOS arm64.
It compiles and runs the model and query examples extracted from the packaged README.
It also checks configured UTF-16 input, sync materialization, async LINQ, and async dictionary creation.

The existing synthetic-format tests continue to check the shared query boundary.
No second production format or public provider API is introduced.
The release guide lists the deferred features.

## Reproduction

Use the commands in the [release guide](release.md).
The local environment used SDK 10.0.403 and runtime 10.0.14.
The Release solution build passes with zero warnings and errors.
All 485 unit tests pass after the documentation update.
The user confirmed that the expanded CI validation passed on Windows, Linux, and macOS.
