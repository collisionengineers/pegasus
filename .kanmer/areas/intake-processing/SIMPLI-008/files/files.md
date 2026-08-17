# Impact — SIMPLI-008

| File / module | Change | Risk |
|---|---|---|
| Core durable-intake contracts | Add a bounded queued-status query keyed by staged receipt ID. | Do not expose leases, storage keys, actors, or exception detail. |
| Infrastructure intake persistence | Compose work, staged receipt, evaluation, receipt, and current case. | Completed work may have no case. |
| Web Upload page | Redirect successful durable submission to staged status. | Duplicates must return the same staged ID. |
| Upload status Razor Page | Authorised four-state view, safe failure label, manual and bounded automatic refresh, final links. | Avoid CSP and terminal refresh-loop regressions. |
| Tests | Prove state mappings, authorization, 404, refresh behavior, and destinations. | Inspect Web state before explicitly draining Worker work. |
| Canonical docs | Record required behavior, UI journey, as-built ownership, and source caller truth. | Do not claim deployment or live execution. |

## Ripple effects

SIMPLI-009 supplies the Worker-only processing boundary. Existing /Received remains the processed-receipt recovery surface.

## Out of scope

No new processing owner, queue, database, deployment, or live operation.
