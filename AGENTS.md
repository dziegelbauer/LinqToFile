# Project guidance

Build the specification with the user before starting implementation. Work incrementally; expect the design to evolve.

Core requirements: .NET 10+, cross-platform, Native AOT compatible, BCL-only runtime, no runtime reflection, and minimize allocations.

Release license: Apache License 2.0 (SPDX: Apache-2.0).

Build and maintain human-readable repository documentation using ASD-STE100 (Simplified Technical English). See the documentation topic for scope and verification. Agent specification notes do not replace user documentation.

Prefer Rider MCP for suitable project, file, analysis, build, and test operations.

Keep this index small. Maintain the topic files as decisions change. Separate confirmed requirements from proposals and future objectives; assistant suggestions are not user decisions.

Read relevant topics before proposing contracts or changing code:
- [Scope and roadmap](docs/spec/scope.md)
- [Phased plan and acceptance criteria](docs/spec/plan.md)
- [Mapping](docs/spec/mapping.md)
- [Reading and errors](docs/spec/reading.md)
- [Sources and query execution](docs/spec/sources.md)
- [Open questions](docs/spec/questions.md)
- [Human documentation](docs/spec/documentation.md)
- [Performance and CI](docs/spec/performance.md)
- [Release preparation](docs/spec/release.md)
