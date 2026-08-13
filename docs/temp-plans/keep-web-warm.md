# Keep Web warm

## Scope

Persist the operator-approved production configuration for the Pegasus Web
Container App: retain one minimum replica, preventing scale-to-zero cold starts.

## Files

- `infra/modules/platform.bicep` — declarative Container App scale setting.
- `docs/architecture.md` — intended topology.
- `docs/operations.md` — live Container Apps evidence boundary.
- `docs/open-decisions.md` — dated cost context whose current-state wording
  otherwise contradicts the changed configuration.

## Sequence

1. Change only Web `minReplicas` from zero to one; keep the one-replica maximum.
2. Align the affected current-state documentation without editing immutable ADRs.
3. Compile the Bicep entry point and inspect the diff.

## Acceptance

- The Bicep template sets Web `minReplicas: 1` and `maxReplicas: 1`.
- No other resource configuration changes.
- Bicep compiles successfully.
- Documentation no longer presents the Web App as scaling to zero.
