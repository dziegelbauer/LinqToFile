# Source project layout validation

## Authorized change
- The user requested moving the runtime and generator projects under src, on a separate branch with a pull request.
- Branch chore/src-layout starts from origin/main at 88363c4, after headerless CSV PR #11.
- Move the complete projects to src/LinqToFile and src/LinqToFile.Generator. Group them under a src solution folder without changing project identities.
- Update test/sample/benchmark references, root README/license package paths, the CI pack command, and active build/release documentation. Preserve historical evidence paths under docs/spec/history.
- Keep package version and runtime/generator behavior unchanged.

## Review and checks
- Compared all 90 moved non-project source/support files byte for byte against the base commit: identical. The generator project file is also unchanged; only the runtime project's root package-asset paths change.
- Manually reviewed all solution, project, workflow, and documentation edits. No C# or script content was edited, so code recommendation inspection is not required for these pure renames. Project configuration and Markdown are exempt from code lint under the standing instruction.
- Rider initially retained old project paths immediately after the filesystem move. Its second build completed successfully with no retained build problems after solution reload.
- macOS arm64: dotnet build LinqToFile.sln -c Release -warnaserror succeeds with zero warnings/errors.
- All 1,336 tests pass, none skipped. The complete JIT sample passes through the new project-reference graph.
- dotnet pack src/LinqToFile -c Release --no-build --no-restore -o artifacts/src-layout-feed succeeds.
- Verify-Package.ps1 with that feed passes using a fresh external two-project consumer/cache under both JIT and Native AOT. Runtime/generator package payload, Apache-2.0 license/metadata, empty runtime dependencies, exact packaged README, and executable examples are verified.
- Active-path search finds no obsolete root project references or pack commands. Sibling references between the two moved projects remain valid. git diff --check passes.
- Hosted branch/PR Windows/Linux/macOS checks run separately. Open the PR for human review; do not merge or enable automatic merging.
