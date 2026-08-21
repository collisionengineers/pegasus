# Proof — PLAT-021 (verified on deployed release 16, 2026-08-21)

Type: command-log. Deployment evidence bundle: [[DELIV-015]] proof.

- Deployed at release 16 via `azd provision` (PR #497 squash `c395839e`; the scheduled-query rule is infrastructure-owned in `infra/modules/platform.bicep`).
- Live readback: `az monitor scheduled-query list -g rg-pegasus-prod` shows `pegasus-prod-application-exceptions` **enabled with a 15-minute window** — the operation-aware query (dedupe by normalized signature + operation; failed-recent-operation branch over 5 minutes; ≥3-distinct-operation persistence; ≥3 minute-bucket uncorrelated branch) replaced the count-all rule. The Web 5xx rule keeps PT5M (the simplification-pass finding, applied).
- The existing Sev1 severity and action group are preserved (bicep diff reviewed; no other alert resources changed).
- Architecture contract tests (`ApplicationExceptionAlertContractTests`) merged and green in CI.
- Live firing behaviour (a real failed operation paging exactly once) is observable only when a genuine failure occurs; the historical-replay fixtures in the tests stand as the deterministic evidence, and the rule's noise reduction is the absence of recovered-operation pages going forward.
