# Files — PLAT-053

> Corrected 2026-08-29 after adversarial verification of PR #613: the
> `ParseEvaSubmission`/`FormatEvaSubmission` pair described here was
> withdrawn, and the out-of-scope list was five files short.

## Touched

- `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` — replace
  the `"pending"`/`"dispatching"`/`"queued"`/`"processing"`/`"completed"`/
  `"failed"` literals for `ExternalWorkItemEntity.State` with the new owner.
  The two `Case.CustodyState = "failed"` assignments (lines 480, 646) are a
  different vocabulary and are left untouched.
- `src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionWorkStore.cs` —
  the same literals, in the terminal checks, the `is not (...)` unknown-state
  guard, the `ExecuteUpdateAsync` claim, the lease-authority check and the
  `EvaSubmissionWorkState -> string` outcome switch. Substitution only; the
  control flow and the switch's `_ => Pending` catch-all are unchanged.
- `src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionQueries.cs` —
  the `"completed"`/`"failed"` terminal-state comparison in
  `GetActivityAsync`.

## New

- `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs` —
  19 lines: six `const string` fields for the persisted
  `ExternalWorkItems.State` codes, and nothing else. No parse or format
  method — mapping the persisted word to a Core enum is a separate concept
  already owned by `EfVehicleLookupWorkStore.MapWorkState`, and a second
  copy of it would have added to the duplication this ticket removes.

## Explicitly not touched (found, out of scope)

The same `ExternalWorkItems.State` literals exist in ten further
Infrastructure classes:

- `EfVehicleLookupWorkStore.cs` — 49, 53, 57, 145, and `MapWorkState` (~422)
- `EfAutomaticEvaSubmissionStore.cs` — 95
- `EfQueuedCustodyProcessor.cs` — 43, 1049, 1076
- `EfOperationsStore.cs` — 287, 460
- `EfCaseWorkflowStore.cs` — 1178-1184
- `EfCaseAcceptanceStore.cs` — 391
- `EfImageIntakeStore.cs` — 212, 632
- `EfLinkedCaseReplacementStore.cs` — 214
- `EfVehicleWorkflowStore.cs` — 134, 903

The last four were missing from the first version of this list, which closed
with "several external-work producers".

These are the same persisted vocabulary but were not named in this ticket's
"Owns" list, and EPIC-011's contract is one ticket per whole file with no
cross-lane edits. Raised as [[PLAT-056]].

## Tests

No test file touched. `EfEvaSubmissionWorkStore` has no test anywhere in the
repository — pre-existing, raised as [[PLAT-057]]. Coverage per changed file
is tabulated in the `plan` doc.
