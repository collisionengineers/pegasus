# Post-implementation report — PLAT-053

## What changed

- `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs`
  (new) — six `const string` codes (`Pending`, `Dispatching`, `Queued`,
  `Processing`, `Completed`, `Failed`) for the persisted
  `ExternalWorkItems.State` vocabulary, plus `ParseEvaSubmission(string,
  int attemptCount)` and `FormatEvaSubmission(EvaSubmissionWorkState)`
  mapping to/from Core's `EvaSubmissionWorkState`.
- `EfExternalWorkStore.cs` — every `ExternalWorkItemEntity.State` literal
  (dispatch claim, requeue, complete, fail, poison, ready-batch query,
  dispatch-candidate comparer, lease-check) now reads
  `ExternalWorkStatePersistence.*`. The two `Case.CustodyState = "failed"`
  assignments (a different, unrelated vocabulary) were correctly left as
  literals.
- `EfEvaSubmissionWorkStore.cs` — the inline terminal/lease-state literal
  checks and the ad hoc `EvaSubmissionWorkState -> string` switch (which
  duplicated the "unknown state" guard) now go through
  `ParseEvaSubmission`/`FormatEvaSubmission`.
- `EfEvaSubmissionQueries.cs` — the `"completed"`/`"failed"`
  terminal-state comparison in `GetActivityAsync` now reads the constants.

Persisted string values are byte-for-byte unchanged; `RetryScheduled` still
persists as `pending`. No migration, no Razor/Pages/OperatorLabels touched.

## Why / reuse

Core has no owning type for the full six-code persistence vocabulary (see
`plan` doc); the closest Core type, `EvaSubmissionWorkState`, is narrower
and already mapped 1:1 in `ParseEvaSubmission`/`FormatEvaSubmission`. The
codebase's existing per-store `ToCode`/`ParseState` convention
(`EfCaseAcceptanceStore`, `EfIntakeReceiptStore`, `EfAiWorkRequestStore`,
`EfApprovedMailboxStore`) doesn't fit because three separate classes need
the same codes — so one small internal static class, not a per-store
private method, is the single owner. No new interface or mapper hierarchy
was introduced.

## Build

`dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0 warnings,
0 errors. Run twice: once by the implementer, once independently verified.

## Tests

`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~ServiceHealthPersistenceTests|FullyQualifiedName~EvaSubmissionPersistenceTests"`

Passed: 33, Failed: 0, Skipped: 1. Run twice (implementer + independent
verification pass); identical result both times.

## Commits

- `8a358ad4` — `refactor(infrastructure): unify external-work state
  literals under one owner (PLAT-053)` — pushed to
  `task/plat-053-external-work-vocabulary`.

## Out-of-scope defects found

A fourth-plus copy of the same `ExternalWorkItems.State` literal set exists
in `EfVehicleLookupWorkStore.cs`, `EfAutomaticEvaSubmissionStore.cs`,
`EfQueuedCustodyProcessor.cs`, `EfOperationsStore.cs`,
`EfCaseWorkflowStore.cs`, and other external-work producers. Not touched:
this ticket's "Owns" list named exactly three files, and folding in five-plus
more files/producers is materially more surface than a "fix"-profile ticket
was scoped for. Recommend a follow-up ticket to complete the consolidation
across every remaining producer/consumer.

## Risks / open questions

None outstanding — the "fold in a fourth copy" language in the working
brief was resolved by treating the ticket's own explicit three-file "Owns"
list as authoritative over a broader open-ended sweep (see plan doc);
scope expansion is flagged as a follow-up ticket rather than absorbed.
