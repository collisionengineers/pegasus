# Plan — INTK-018: resolve Unidentified items when their receipt reaches a real destination

Branch `task/intk-018-unidentified-resolution` from origin/dev (6e164f60), worktree `../pegasus-worktrees/intk-018`, PR → `dev`. Independent of INTK-015's branch (different files).

## Steps

1. **Extract the one destination-resolution owner.** New Core class `ReconcileUnidentifiedDestinations` (beside `ReconcileGroupedImageIntake`, same shape): `ResolveForReceiptAsync(receipt)` is the resolve half of `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` moved — not copied — out (reuses `ProcessIntake.IsUnidentifiedEligible`, `IUnidentifiedStore.GetByOriginAsync`, `IResolveUnidentified`, the `Automation("intake-processing")` actor and `intake-unidentified-reconcile:{receiptId}:{version}` op key exactly as today). `ExecuteAsync(max)` sweeps open receipt-origin items (store `ListAsync(Open)`), loads each receipt via `IIntakeReceiptQueries`, and calls the same method; counts candidates/resolved/failures like `ReconcileGroupedImageIntakeResult`.
2. **Delegate from the pipeline.** `ProcessQueuedIntake` ctor: replace optionals `IResolveUnidentified`/`IUnidentifiedStore`/`IImageIntakeQueries` with `ReconcileUnidentifiedDestinations?`; the register half of `SynchronizeUnidentifiedAsync` is unchanged. In-pass resolution behaviour is preserved (the sweep is the new recovery for receipts promoted outside their own pass — production U7's shape).
3. **Wire the sweep.** Register in Infrastructure DI beside the Unidentified use cases; run from `StagedArtifactReconciliationFunction` after `ReconcileGroupedImageIntake` on the same existing timer (the INTK-011 precedent: no new schedule), max 50, with a LoggerMessage.
4. **FRD-02** — one sentence in the Unidentified contract: an open item whose origin receipt reaches a real destination is automatically resolved with the destination recorded in its history.
5. **Tests** (files doc lists them): Core fakes for the mapping + sweep; integration U7-shape (register → promote → sweep → Resolved with ImageIntake reference in history, replay-safe); integration pending-window regression (GroupPending member has no U row while pending and none after the group registers). Focused: new Core class tests, `UnidentifiedPersistenceTests`, `UnidentifiedContractsTests`, plus build zero-warning; `Test-MigrationGrants.ps1` + `Test-AzureDeploymentPlan.ps1 -Mode Local` (no migration expected — both must simply stay green).

## Half (b) disposition (recorded, per the ticket's "check who creates U rows at staging")

Verified on dev (files doc, "Verified facts"): no U row is created while a work item is pending/retrying — creation happens only at terminal decisions, and the production "opened at staging" timestamps are the deliberate `CreatedAtUtc = ReceivedAtUtc` backdating plus release-13 code that predates the INTK-011 GroupPending deferral. The honest fix half (b) therefore needs is the regression test pinning that contract, not a new creation-time mechanism; the pending-window integration test is that pin. No operator-surface filter is added — there is nothing pending to filter out once creation is terminal-only.

## Acceptance

- Open U item whose receipt was promoted to an ImageIntake (by any path, in any pass) is resolved by the sweep with the destination in its history; same for a CaseCreated receipt.
- Still-unresolved receipts (Unsupported/TechnicalFailure/needs-sorting non-image, or image material with no registration) keep their open items — the sweep never force-closes real work.
- Pending group members never gain a U row (regression).
- Build zero-warning; focused suites green.
