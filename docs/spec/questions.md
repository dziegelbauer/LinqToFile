# Open questions

Ask in small batches; move answers into the relevant topic file.

1. RESOLVED in phase 1: generated static-interface mapping works across assemblies under JIT and Native AOT, including single-package consumption. Partial models are required; runtime registration is removed.
2. RESOLVED in phase 3: inheritance diagnostics cover overrides, cross-kind annotation conflicts, hidden fields/methods/properties, inherited required properties, and generic base substitution.
3. RESOLVED in phase 3: exact property mapping collisions are compiler errors; comparison-dependent collisions throw from WithHeaderComparison. File/header/record errors remain deferred.
4. RESOLVED in phase 2: field/record limits and decoded buffer size are implemented and tested. Disabled BOM detection still removes the configured encoding's matching BOM without switching encodings.
5. RESOLVED in phase 3: BCL AdjustToUniversal without assumed-zone flags identifies explicit offsets without local-zone conversion. DateTime rejects offset-bearing input; DateTimeOffset preserves explicit offsets. Tests cover two host time zones and range edges. Missing date components follow standard BCL parsing defaults. Exact-format annotations remain deferred.
6. Validate internal buffer defaults and pooling through benchmarks during implementation. Public limits and ownership rules are recorded in reading.md; no further user decision on tuning is required initially.
7. Which async API signatures can reuse BCL operators without wrappers? ToDictionaryAsync supports key/value selectors, comparer, and cancellation; duplicate keys throw.
8. ASD-STE100 Issue 9 is the approved reference. Specialized review tooling remains a future implementation/release choice; examples have automated drift checks.
9. Phase 1 uses xUnit and Roslyn compilation tests, with a BCL benchmark harness. Specialized benchmark tooling can be selected when broader performance work begins.
