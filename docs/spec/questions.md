# Open questions

Ask in small batches; move answers into the relevant topic file.

1. RESOLVED in phase 1: generated static-interface mapping works across assemblies under JIT and Native AOT, including single-package consumption. Partial models are required; runtime registration is removed.
2. Validate generator diagnostics for inheritance: most-derived overrides map once, Column may replace inherited Column, and inherited Column/Ignore cross-kind conflicts are compiler errors. Ambiguous hidden properties are rejected.
3. Which mapping conflicts can be detected at compilation, and which depend on runtime header comparison? Configuration errors throw immediately; file/header/record errors are deferred to enumeration.
4. RESOLVED in phase 2: field/record limits and decoded buffer size are implemented and tested. Disabled BOM detection still removes the configured encoding's matching BOM without switching encodings.
5. Validate concrete parsing against the agreed date/time contracts: DateTime rejects offsets and retains Unspecified for offset-free input; DateTimeOffset requires an explicit offset and preserves it. Exact-format annotations are deferred.
6. Validate internal buffer defaults and pooling through benchmarks during implementation. Public limits and ownership rules are recorded in reading.md; no further user decision on tuning is required initially.
7. Which async API signatures can reuse BCL operators without wrappers? ToDictionaryAsync supports key/value selectors, comparer, and cancellation; duplicate keys throw.
8. ASD-STE100 Issue 9 is the approved reference. Specialized review tooling remains a future implementation/release choice; examples have automated drift checks.
9. Phase 1 uses xUnit and Roslyn compilation tests, with a BCL benchmark harness. Specialized benchmark tooling can be selected when broader performance work begins.
