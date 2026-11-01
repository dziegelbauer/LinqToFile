# Release preparation

## Current artifact

The current local package version is `0.1.0-alpha.9`.
It is the local prerelease base version.
Successful main CI runs append a unique run suffix and publish a prototype prerelease to nuget.org.
See [publishing setup](publishing.md) for the one-time account configuration and feature-branch workflow.
The API can change before a stable release.

The package contains the runtime, source generator, README, and Apache-2.0 license.
The runtime targets .NET 10 and has no NuGet dependencies.
The generator runs during compilation. Applications do not register mappers.
Each project that declares models must reference the package.

## Prepare the artifact

From the repository root, run these commands:

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore
dotnet pack LinqToFile -c Release --no-build --no-restore -o artifacts/feed
```

Run the external consumer check with PowerShell 7:

```sh
pwsh -NoProfile -File eng/Verify-Package.ps1 -RuntimeIdentifier osx-arm64
```

Use `win-x64` on the Windows runner and `linux-x64` on the Linux runner.
The native compiler for that platform is necessary.
PowerShell is a development tool for this check. The library does not require it.

The script checks the package identity, repository URL, Apache-2.0 license, and DLL payload.
It rejects runtime package dependencies.
It compares the packaged license with the repository license.
It then creates two projects outside the repository and uses a fresh package cache.
The model project references the package. The application references the model project.
Neither project references the runtime or generator source projects.

The check compiles the model from the packaged README.
It runs the README query, sync reads, async LINQ, dictionary materialization, and configured encoding.
It also compiles XLSX models in the separate model assembly.
XLSX checks cover both variants, cached formulas, temporal values, shared-string spill, async queries, and non-seekable input.
It repeats the application run with Native AOT.
Successful execution prints this message:

```text
Package validation passed: LinqToFile 0.1.0-alpha.9 (osx-arm64)
```

The script removes its temporary workspace after execution.
Use `-KeepWorkspace` to keep that workspace for investigation.
Use `-PackageDirectory` to select another local feed directory.

## Review before publication

1. Select the release version and confirm the package author metadata.
2. Update `Directory.Build.props`, the installation guide, and this guide together.
3. Build the artifact from the intended release commit.
4. Confirm that all platform jobs pass for that commit.
5. Review the package README, license, repository link, and release notes.
6. Confirm the target NuGet feed and package ownership.

Public prerelease publication is automatic after successful main CI.
GitHub release creation and stable releases remain separate actions.
NuGet metadata follows the [MSBuild package reference](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets).

## Scope of the initial release

CSV implements the agreed initial reader scope. Headers are required.
The package also includes [sync/async XLSX reading](../../xlsx.md) with shared-string spill, cached formulas, native temporal conversions, and resource limits.
XLSX accepts paths, streams, and sync/async factories.
Non-seekable XLSX input requires temporary storage. ZIP directory loading and seeks are synchronous.
The reader supports strict parsing, generated property mapping, and sync/async streaming.
It supports owned records and full list, array, or dictionary materialization.
It does not provide a custom `IQueryable` provider, file writing, or change tracking.

Other formats, headerless files, custom converters, and more model shapes remain future objectives.
Per-column error recovery, configurable missing columns, and opt-in raw diagnostic values are also future objectives.
The shared source, query, schema, and mapping layers remain separate from the CSV adapter.
A second real format must validate a public provider API before that API is published.
