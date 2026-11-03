# Human documentation

## Confirmed
- Build and maintain human-readable documentation inside the repository.
- Use ASD-STE100 (Simplified Technical English); the user's earlier STS-100 reference was corrected.
- The user approved Issue 9 (January 2025) as the reference.
- Treat documentation as a top-level project requirement.
- Human documentation contains only current information needed to use the library; omit phase/stage history, commit IDs, implementation chronology, and validation logs.
- docs/README.md indexes task-based usage guides. docs/roadmap.md lists only unimplemented future plans; do not mix completed work into that roadmap.
- Keep agent requirements in docs/spec, maintenance instructions in docs/spec/maintenance, and historical validation/measurement evidence in docs/spec/history. Update their relative links when moving files.

## Confirmed coverage
- README and getting started.
- Mapping and annotation reference.
- CSV options, conversions, and error behavior.
- Sync/async queries, materialization, cancellation, and source ownership.
- Native AOT setup and supported model restrictions.
- Performance guidance and measured allocation behavior.
- Contributor architecture and maintenance instructions are separate internal references, not part of the user guide.
- Documentation updates and verification of runnable examples are completion criteria for every implementation slice.

## Verification
- Identify and consult the authoritative edition before claiming conformance; do not invent standard rules.
- Keep API examples aligned with implementation and identify planned APIs as proposals.
- Review documentation alongside implementation changes.
- After all CSV-writing stages, review the main README and NuGet package information as introductions: explain why to use the library, its capabilities, installation, quick examples, and links to detailed guides. This final review is an explicit user requirement.
- The introductory review is implemented: one root README now serves GitHub and NuGet, with absolute documentation links. Package validation requires exact equality and executes all five C# blocks externally; QuickStartSample and drift checks cover repository examples. Do not reintroduce a separate package introduction that can drift.
- During the token PR the user requested another README refresh: friendly, benefit-led text that encourages adoption. Keep installation, quick examples, and guide links; move header requirements, model-shape restrictions, write mechanics, and other detailed rules to the guides. Keep capability claims accurate. Preserve the shared GitHub/NuGet introduction and its runnable-example checks.

## Unresolved
- Specialized STE review tooling and release certification, if any. The official Issue 9 PDF was consulted for writing rules and dictionary usage during phase 1. Human documentation uses short sentences, active voice, and a technical glossary. Runnable README snippets are checked against the sample by a test. No certification is claimed.
- Exact release documentation checks.
