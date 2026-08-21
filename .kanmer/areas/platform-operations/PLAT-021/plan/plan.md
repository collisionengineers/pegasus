# Approach

Keep one scheduled-query alert and replace count-all KQL with an operation-aware query. Use a 15-minute rule window, an explicit five-minute failed-request branch, normalized signatures, and minute buckets for uncorrelated telemetry. Preserve Sev1 and the existing action group.

# Steps

1. Define readable multiline KQL in platform.bicep that normalizes exception signature and operation identity.
2. Deduplicate correlated exceptions by operation/signature and join AppRequests to identify failed Web/Worker operations in the last five minutes.
3. Add a persistence branch for the same signature across at least three distinct operations in 15 minutes.
4. Bucket operationless signatures by one minute and require at least three distinct buckets.
5. Return a numeric AlertCount plus diagnostic dimensions and configure the rule with a 15-minute window.
6. Add architecture assertions and deterministic historical/replay fixtures for permission incidents, duplicate telemetry, recovered deadlock, repeated operations, and operationless crash loops.
7. Run Bicep compilation, focused tests, Release build/full non-corpus tests, and simplification pass.
8. Open a PR to dev. After review/merge, request exact alert-rule deployment approval; then compare historical KQL and refresh operations.md.

# Governing docs

- FRD-12 / OPS-08: preserves actionable production failure alerting while removing recovered-operation noise.
- ADR-0002: retains the accepted Azure Monitor and action-group architecture.

# Risks and mitigations

- AppRequests correlation gaps: the distinct-operation persistence branch and operationless buckets retain detection.
- Signature cardinality: normalize using stable exception type and outer message, not stack traces.
- KQL escaping in Bicep: use the repository's multiline string convention and compile the template.
- Unproven live behavior: require read-only historical comparison before approved deployment.

## Simplification pass — 2026-08-21

Reuse: retained the existing alert resource/action group. Simplification: one KQL rule with three explicit branches. Efficiency: deduplicates before persistence aggregation. Altitude: no application instrumentation changes. Finding applied: corrected the resource-local window so Web 5xx remains PT5M and only application exceptions use PT15M.
