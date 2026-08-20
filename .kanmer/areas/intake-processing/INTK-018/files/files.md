# Files — INTK-018 Unidentified resolution

Verified read-only in worktree `../pegasus-worktrees/intk-018` at `origin/dev` (6e164f60). Independent of [[INTK-015]] (branched from origin/dev; touches different files — no overlap with ImageIntakeAutomation/EfImageIntakeStore/UploadGroupStatus/bicep).

## Verified facts (read-only checks, recorded per the rails)

- **U rows are NOT created at staging on current dev.** `ProcessIntake.BuildUnidentifiedRegistrationRequest` (src/Pegasus.Core/Intake/ProcessIntake.cs:309) deliberately sets `CreatedAtUtc = receipt.ReceivedAtUtc` ("the queue and detail UI order… by when the source arrived") — the production "U7–U10 opened at 02:54:39" is that backdating, not an insert-at-staging. Actual inserts happen only at terminal points: `RegisterUnidentifiedIfTerminalAsync` after a persisted terminal evaluation (ProcessIntake.cs:273, gated by `IsUnidentifiedEligible` which excludes image-only NeedsSorting), `SynchronizeUnidentifiedAsync` after image automation had its chance (DurableIntake.cs:695 — skipped entirely on `GroupPending`, DurableIntake returns RetryScheduled first), and the 2-hour poison escape in `ReconcileGroupedImageIntake` (:95). A pending/retrying member gets no U row on dev.
- **The resolution gap is real and one-sided.** The only resolver of stale open items is the resolve half of `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` (DurableIntake.cs:713-775), which runs only inside a processing/replay pass of THAT receipt. A receipt promoted by a sibling's pass (production U7), or any historic open row, is never revisited — nothing sweeps open Unidentified items against their receipts' current state. Confirmed: no other caller of `IResolveUnidentified` outside the staff UI (`ResolveUnidentified` use case) exists.
- `IUnidentifiedStore.GetByOriginAsync` doc already anticipates this: "used to reconcile a stale open item once its source receipt reaches a different, non-Unidentified outcome."
- `UnidentifiedValidation.ValidateResolve` requires Staff or Automation; the existing convention is `ActionActor.Automation("intake-processing")` with op key `intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}` (DurableIntake.cs:755-770). `ResolveUnidentified` validates the destination exists; `EfUnidentifiedStore.ResolveAsync` writes the history entry with TargetKind/TargetId/TargetReference — INTK-007's destination-in-history contract comes free.

## Change set

- **New** `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` — the one owner of "resolve an open Unidentified item whose origin receipt now has a real destination":
  - `ResolveForReceiptAsync(IntakeReceipt, ct)` — the mapping moved verbatim out of `SynchronizeUnidentifiedAsync` (still-eligible → leave open; `CaseCreated`+`CurrentCaseId` → InstructionCase; `ImageIntakeRegistered` → ImageIntake via `IImageIntakeQueries.GetByOriginReceiptAsync`), same actor/op-key/reason conventions.
  - `ExecuteAsync(maximumItems, ct)` — the sweep: open receipt-origin items → load receipt → `ResolveForReceiptAsync`; counts candidates/resolved/failures, modelled on `ReconcileGroupedImageIntake`.
- `src/Pegasus.Core/Intake/DurableIntake.cs` — `ProcessQueuedIntake` ctor swaps the three resolve-only optionals (`IResolveUnidentified`, `IUnidentifiedStore`, `IImageIntakeQueries`) for `ReconcileUnidentifiedDestinations?`; `SynchronizeUnidentifiedAsync` keeps its register half and delegates the resolve half (one list per concept — the mapping moves, it is not copied).
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — `AddScoped<ReconcileUnidentifiedDestinations>()` beside the existing Unidentified registrations (:102-104), so Web and Worker hosts and the test factory all resolve it.
- `src/Pegasus.Worker/IntakeFunctions.cs` — `StagedArtifactReconciliationFunction` also runs the new sweep on the same existing timer (deliberately not a new schedule), with its own LoggerMessage.
- `docs/frd/frd-02-intake-and-source-identity.md` — Unidentified section: an open item whose origin receipt reaches a real destination is resolved automatically by the product's own reconciliation, destination recorded in history.

## Tests

- **New** `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` — fakes: promoted-to-ImageIntake receipt resolves its open item with ImageIntake target+reference; CaseCreated receipt resolves to InstructionCase; still-eligible receipt leaves the item open; group-origin items skipped; resolve failure counted, never thrown.
- `tests/Pegasus.IntegrationTests/UnidentifiedPersistenceTests.cs` (or new sibling file) — the U7 shape end-to-end: register a U item for a real needs-sorting image receipt, promote the receipt to an ImageIntake, run the sweep, assert Resolved + history row carrying the ImageIntake reference; replay-safe second run.
- Half (b) regression in the same integration file: a group member processed while its sibling is still staged (GroupPending) has NO Unidentified row during the pending window, and none after the group resolves to registration.

## Out of scope (stated)

- Production readback (U7 resolved by the deployed sweep) — deploy verification.
- Group-origin (`UnidentifiedOriginKind.SubmissionGroup`) items: nothing registers them today (verified — only `UploadOutcome` reads that origin); the sweep skips them explicitly rather than inventing behaviour.
- The one-per-member U fan-out for terminally-unresolvable groups (FRD case 4 "intact group as one unit") — the INTK-007/INTK-015 surface question, not this ticket.
