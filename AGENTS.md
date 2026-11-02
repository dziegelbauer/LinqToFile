# Project guidance

Build the specification with the user before starting implementation. Work incrementally; expect the design to evolve.

Core requirements: .NET 10+, cross-platform, Native AOT compatible, BCL-only runtime, no runtime reflection, and minimize allocations.

Use enums or other typed options for fixed configuration choices; do not use magic strings. Strings remain appropriate for user-provided values such as paths, column headers, and worksheet names.

Release license: Apache License 2.0 (SPDX: Apache-2.0).

Build and maintain human-readable repository documentation using ASD-STE100 (Simplified Technical English). See the documentation topic for scope and verification. Human documentation describes current usage only; omit implementation history and validation logs. Keep unimplemented plans in docs/roadmap.md. Agent specifications, maintenance instructions, and historical evidence stay under docs/spec and are not user guides.

Prefer Rider MCP for suitable project, file, analysis, build, and test operations.

Do all future work on feature branches and merge through pull requests into main. Successful main CI runs publish prototype prerelease packages to nuget.org; branch and pull-request runs validate only. See docs/spec/release.md for versioning and publishing setup.
All merges to main require human approval. Open pull requests for review; never merge them or enable automatic merging yourself.
Finish work with a clean Git worktree: commit all repository changes, including agent/spec notes, and verify git status before handing work back. Keep separate topics in separate commits; never discard user changes to obtain a clean status.

Keep this index small. Maintain the topic files as decisions change. Separate confirmed requirements from proposals and future objectives; assistant suggestions are not user decisions.

Read relevant topics before proposing contracts or changing code:
- [Scope and roadmap](docs/spec/scope.md)
- [Phased plan and acceptance criteria](docs/spec/plan.md)
- [Mapping](docs/spec/mapping.md)
- [Writing and row insertion planning](docs/spec/writing.md)
- [Reading and errors](docs/spec/reading.md)
- [Sources and query execution](docs/spec/sources.md)
- [Open questions](docs/spec/questions.md)
- [Human documentation](docs/spec/documentation.md)
- [Performance and CI](docs/spec/performance.md)
- [Release preparation](docs/spec/release.md)
- [XLSX planning](docs/spec/xlsx.md)
