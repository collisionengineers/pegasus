# Open questions — MAIL-07

No unresolved product question once MAIL-23 supplies the canonical folder key. TICK-048 is archived, but its confirmation behaviour is already absorbed by FRD-08 and UI-10; implement that behaviour here rather than reviving a parallel ticket.

## Parked (explicitly deferred)

- [x] **Should MAIL-07 perform real Outlook/Graph/cloud activation and live verification?** — No. Resolved by the operator on 2026-08-19. Do not move any live Outlook message for this ticket. Verify confirmation, exact-message scope, destination validation, idempotency, stale identity, success/failure recording, and staff-only retry with local/integration tests and Graph fakes. Report the feature as not live-mutation-verified; any future real move requires fresh approval for the exact mailbox, disposable message, destination folder, and operation.
