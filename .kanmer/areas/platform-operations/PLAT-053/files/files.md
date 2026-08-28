# Files — PLAT-053

## Touched

- `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` — replace
  the `"pending"`/`"dispatching"`/`"queued"`/`"processing"`/`"completed"`/
  `"failed"` literals for `ExternalWorkItemEntity.State` with the new owner.
  The two `Case.CustodyState = "failed"` assignments (lines 480, 646) are a
  different vocabulary and are left untouched.
- `src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionWorkStore.cs` —
  same literals, plus the inline `EvaSubmissionWorkState` <-> string mapping
  that duplicated unknown-state validation.
- `src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionQueries.cs` —
  the `"completed"`/`"failed"` terminal-state comparison in
  `GetActivityAsync`.

## New

- `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs` —
  the single internal owner: string constants for the six persisted
  `ExternalWorkItems.State` codes, plus `ParseEvaSubmission` /
  `FormatEvaSubmission` mapping to/from Core's `EvaSubmissionWorkState`.

## Explicitly not touched (found, out of scope)

A fourth-plus set of the same `ExternalWorkItems.State` literals exists in:

- `EfVehicleLookupWorkStore.cs`
- `EfAutomaticEvaSubmissionStore.cs`
- `EfQueuedCustodyProcessor.cs`
- `EfOperationsStore.cs`
- `EfCaseWorkflowStore.cs`
- several external-work producers

These are the same persisted vocabulary but were not named in this ticket's
"Owns" list. Folding them in here would roughly double the diff and touch
files no reviewer of this ticket scoped. Left as a follow-up (see plan).
