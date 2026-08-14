# Impact — TICK-017 (DOC-01)

Like INT-25, the DOC-01 mechanism is **already implemented and caller-proved
locally** (against Box fakes). The four boundary behaviours — immutable Case/PO
naming, response-loss-safe binding, fail-closed conflict handling, human
reasoned recovery — exist and are tested. This ticket's real footprint is the
acceptance/proof record, plus — only if the user grants live approval — wiring
the approved disposable Box test subtree into a separate integration-test
profile. No production business code needs to change.

| File / module | Change | Risk |
|---|---|---|
| `src/Pegasus.Core/Custody/CustodyContracts.cs` | None — contract record (`ICaseCustody` port :64; `RetryCaseCustody` reasoned recovery :329-379; `CustodyRetryPolicy.Decide` :246) | Working fail-closed policy; out of scope |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | None — contract record (create/reconcile `GetOrCreateBoundFolderAsync` :645-731; predeclared-owner staging + ETag promotion; occupied-name/duplicate/wrong-type/trashed throws) | Idempotent create protocol is correctness-critical; do not touch |
| `src/Pegasus.Infrastructure/Custody/CustodyNames.cs` | None — contract record (`SafeName` deterministic host-independent mapping :16-31) | Naming determinism is load-bearing; out of scope |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | None by default; **only if live approval granted**, add a separate Box integration-test profile binding the approved disposable subtree `392761581105` (never the production root) | HIGH if attempted without explicit per-target approval — live Box mutation; gated behind user sign-off |
| `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` + `EfQueuedCustodyProcessor.cs` | None — contract record (Worker dispatch → `IProcessQueuedCustody` → `CreateCaseRootAsync`) | — |
| `.kanmer/…/TICK-017/plan.md, checklist.md, proof.md` | **Written** — deliverable: contract/caller/failure/test record; replace placeholder proof ("Operator confirmed") with the real local-tier evidence; record the pending live tier | Low — documentation/proof only |
| `docs/operations.md` (evidence-tier row :96) / `docs/capabilities.md` (:167) | **Possibly** — only to record an explicitly-accepted live-tier deferral, with user sign-off | Low but authoritative-doc edit — needs sign-off |

## Ripple effects

- **Tests to run as proof (no change to them):** `ProductionBoxCustodyTests`
  (immutable naming, response-loss reconcile, fail-closed no-mutation),
  `CustodyOutboxIntegrationTests` (outbox→worker→processor + reasoned staff
  retry taxonomy), `ProductionCompositionTests` (Box adapter resolves from the
  production composition, no network). Their pass output is proof.md's local tier.
- **Live tier (requires-live-approval):** wiring the disposable test subtree
  `392761581105` (runbook :754) into a Box integration-test profile, a one-off
  live create/reconcile smoke, then operator acceptance. Each is a separate,
  explicitly-approved step; none can run under default local/CI composition
  (which resolves `LocalCaseCustody`/`UnavailableCaseCustody`).
- **Blocked-by INT-25 (TICK-012):** the Case/PO reference the folder is named
  from is minted by INT-25's acceptance store; confirm `SafeName` accepts the
  final reference forms (`a.`/`ap.`, `QDOS…`, ≤120 chars) once INT-25's contract
  is pinned.

## Out of scope

- Any edit to `BoxCaseCustody` / `CustodyNames` / the retry policy — proven,
  invariant-bearing; changing them is a stop condition here.
- Any live Box mutation without explicit per-target user approval naming the
  disposable subtree and operation (CLAUDE.md: "Local alpha work must not mutate
  … any Box location"; live-operation approval matrix).
- Production root `405543781910` — never used for test mutation; hard-pinned in
  `BoxCustodyOptions.Create` and left untouched.
- DOC-02 / DOC-03 (document content store, Blob staging) — adjacent capabilities,
  separate tickets.
