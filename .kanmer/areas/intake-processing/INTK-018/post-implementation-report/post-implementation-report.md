# Post-implementation report — INTK-018

Branch `task/intk-018-unidentified-resolution` (worktree `../pegasus-worktrees/intk-018`), rebased onto origin/dev (92584841), 3 commits `bbb7b6d4..77bb1306`, PR → `dev`.

## What shipped

**Half (a) — automatic resolution.** New Core class `ReconcileUnidentifiedDestinations` (src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs) is now the ONE owner of INTK-007's supersession rule:

- `ResolveForReceiptAsync(receipt)` — the resolve half of `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` moved (not copied) out; `ProcessQueuedIntake` delegates to it (ctor swaps the three resolve-only optionals for the component; the two direct test constructions stop before the optionals, verified). Identical conventions preserved: `Automation("intake-processing")` actor, `intake-unidentified-reconcile:{receiptId}:{version}` operation key, destination-existence validation via the existing `ResolveUnidentified` use case, destination recorded in the item's history by `EfUnidentifiedStore.ResolveAsync` — INTK-007's history contract comes free.
- `ExecuteAsync(50)` — a bounded sweep over open receipt-origin items, run from `StagedArtifactReconciliationFunction` on the SAME existing timer as `ReconcileGroupedImageIntake` (the INTK-011 precedent — no new schedule), logging candidates/resolved/failures. This is what recovers a receipt promoted OUTSIDE its own processing pass: a sibling group member's registration (production U7's exact shape), a staff action, or a historic stale open row. Group-origin items are skipped explicitly (nothing registers them today — verified). A receipt that is still legitimately unidentified is never force-closed.
- Registered in Infrastructure DI beside the other Unidentified use cases (deliberate deviation from the Worker-DI siblings, reason recorded in the plan's simplification section).
- FRD-02 Unidentified section now states the automatic-resolution contract.

**Half (b) — no Unidentified while pending.** Resolved by verification, not new machinery, and pinned by a regression test. Verified facts (recorded in the ticket's files doc): on current dev, U rows are created only at terminal decisions — `RegisterUnidentifiedIfTerminalAsync` after a persisted terminal evaluation, `SynchronizeUnidentifiedAsync` only when the pass is not `GroupPending` (the INTK-011 deferral returns first), and the 2-hour poison escape. The production "U7–U10 opened at 02:54:39" is the deliberate `CreatedAtUtc = receipt.ReceivedAtUtc` backdating (queue orders by arrival time) plus release-13 code predating the INTK-011 deferral — not an insert-at-staging on dev. The new integration test `APendingGroupMemberNeverGainsAnUnidentifiedRow` pins the contract: no open U row during the pending window and none after the group resolves to registration.

## Test evidence (exact counts)

- Core: new `ReconcileUnidentifiedDestinationsTests` **6/6**; full `Pegasus.Core.Tests` **713/713**.
- Integration (focused): new `UnidentifiedReconciliationTests` **2/2** — the U7 shape end-to-end (open item → receipt promoted outside its pass → sweep resolves with `ImageIntake` target + reference in the history row → second sweep is a 0/0/0 no-op) and the pending-window regression; `UnidentifiedPersistenceTests` + `GroupedImageIntakeConcurrencyTests` + `UploadOutcomeQueriesTests` **15/15**; post-simplification re-run of `UnidentifiedReconciliationTests` + `GroupedImageIntakeConcurrencyTests` **4/4**.
- `Pegasus.ArchitectureTests` **97/97** (timer-function dependency/logging tests updated for the new reconciler).
- `Test-MigrationGrants.ps1` pass (54 files; no migration in this change); `Test-AzureDeploymentPlan.ps1 -Mode Local` pass; build 0 warnings.

## Deliberately left out / for the reviewer

- **Production readback (U7 resolved by the deployed sweep) is deploy verification**, not code — the sweep is designed to do exactly that on its first production timer tick.
- Group-origin Unidentified items: the sweep skips them; whatever eventually registers one owns its resolution shape (nothing does today — verified only `UploadOutcomeQueries` reads that origin).
- The per-member U fan-out for terminally-unresolvable groups (FRD case 4 "intact group as one unit") remains as-is — INTK-007/INTK-015 surface territory, out of this ticket's scope.
- Composes with [[INTK-015]] but does not depend on it: once both merge, group members flipped in the registration transaction get their stale items resolved by this sweep, and the group-aware `GetByOriginReceiptAsync` makes each member resolve to the one intake.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md` (Unidentified destination and reference section) — the only doc changed; operator-notes untouched.
