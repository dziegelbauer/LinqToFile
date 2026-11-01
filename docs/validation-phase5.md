# Phase 5 validation

## Status

Local checks pass on macOS arm64.
The repository is published on GitHub.
The first reported Windows failure occurred during package-consumer restore.
Phase 5 remains open until the hosted platform jobs pass.

## Local results

- The Release solution build has zero warnings and errors.
- All 485 tests pass.
- Sync and async allocation tests read one million generated records.
- Project-reference and alpha.5 package Native AOT samples publish and run.
- Repeated benchmarks cover parsing, mapping, sync/async streaming, strings, large fields, and collections.
- Buffer trials cover 512, 4096, and 16384 decoded characters.
- The CI file passes a local YAML syntax check. The Windows correction awaits hosted confirmation.

The local SDK was 10.0.403. The runtime was 10.0.14.
See [performance measurements](performance.md) for raw samples and measurement limits.

## GitHub Actions

The [workflow](../.github/workflows/ci.yml) runs on pushes, pull requests, and manual requests.
It grants read access to repository contents.
It does not publish a package or create a release.

| Runner | Runtime identifier | Hosted result |
|---|---|---|
| Ubuntu 24.04 | linux-x64 | Pending |
| Windows 2025 | win-x64 | Package-consumer restore failed; correction awaits rerun |
| macOS 15 | osx-arm64 | Pending |

Each job builds the solution and runs all tests.
It runs the JIT sample and publishes and runs the Native AOT sample.
It repeats Native AOT validation with a fresh local package cache.
The package consumer uses an explicit restore with a dedicated NuGet configuration file.
The next publish uses `--no-restore` with the same cache path.
This avoids passing feed URLs through `dotnet publish --source`.
The Windows log reported NU1301 with `https:\api.nuget.org\v3\index.json` inside a local project path.
The corrected restore and publish commands pass under PowerShell on macOS arm64 with a fresh package cache.
The resulting Native AOT sample prints the expected output.
This local check does not replace the Windows rerun.
This checks that the package supplies the generator to the separate model project.
Test reports and benchmark JSON are uploaded as job artifacts.
Benchmark timing is informational. There is no machine-specific time limit for a scan.

The runner labels follow the [GitHub runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).
Linux installs the [Native AOT build prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).
Other .NET platforms remain compatibility targets. This matrix does not test every platform and architecture.

## Commands used locally

```sh
dotnet build LinqToFile.sln -c Release -warnaserror
dotnet test tests/LinqToFile.Tests -c Release --no-build --no-restore --nologo
dotnet publish samples/NativeAot -c Release -r osx-arm64 -p:TreatWarningsAsErrors=true -o artifacts/phase5-aot
./artifacts/phase5-aot/NativeAot
dotnet pack LinqToFile -c Release --no-build --no-restore -o artifacts/feed
dotnet restore samples/NativeAot -r osx-arm64 -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/phase5-package-cache" --configfile .github/package-validation.nuget.config
dotnet publish samples/NativeAot -c Release -r osx-arm64 --no-restore -p:UseLocalPackage=true -p:TreatWarningsAsErrors=true -p:RestorePackagesPath="$PWD/artifacts/phase5-package-cache" -o artifacts/phase5-package-aot
./artifacts/phase5-package-aot/NativeAot
```

The samples print the expected sync, conversion, and async results.
The runtime has no new package dependencies.
