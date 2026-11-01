# Human documentation

## Confirmed
- Build and maintain human-readable documentation inside the repository.
- Use ASD-STE100 (Simplified Technical English); the user's earlier STS-100 reference was corrected.
- The user approved Issue 9 (January 2025) as the reference.
- Treat documentation as a top-level project requirement.

## Confirmed coverage
- README and getting started.
- Mapping and annotation reference.
- CSV options, conversions, and error behavior.
- Sync/async queries, materialization, cancellation, and source ownership.
- Native AOT setup and supported model restrictions.
- Performance guidance and measured allocation behavior.
- Contributor architecture and design decisions.
- Documentation updates and verification of runnable examples are completion criteria for every implementation slice.

## Verification
- Identify and consult the authoritative edition before claiming conformance; do not invent standard rules.
- Keep API examples aligned with implementation and identify planned APIs as proposals.
- Review documentation alongside implementation changes.

## Unresolved
- Specialized STE review tooling and release certification, if any. The official Issue 9 PDF was consulted for writing rules and dictionary usage during phase 1. Human documentation uses short sentences, active voice, and a technical glossary. Runnable README snippets are checked against the sample by a test. No certification is claimed.
- Exact release documentation checks.
