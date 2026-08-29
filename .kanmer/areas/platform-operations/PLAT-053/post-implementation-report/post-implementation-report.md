# Post-implementation report — PLAT-053

> Rewritten 2026-08-29 after adversarial verification of PR #613. The first
> version described a `ParseEvaSubmission`/`FormatEvaSubmission` pair that no
> longer exists, and cited a test class that exercises none of the changed
> code. Both are corrected below; the dispositions are in the `plan`
> document under "Review findings — dispositions (round 2)".

## What changed

- `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs`
  (new, 19 lines) — six `const string` codes (`Pending`, `Dispatching`,
  `Queued`, `Processing`, `Completed`, `Failed`) for the persisted
  `ExternalWorkItems.State` vocabulary. Constants only: no parse or format
  method, no interface, no mapper.
- `EfExternalWorkStore.cs` — every `ExternalWorkItemEntity.State` literal
  (dispatch claim, requeue, complete, fail, poison, ready-batch query,
  dispatch-candidate comparer, lease check) now reads
  `ExternalWorkStatePersistence.*`. The two `Case.CustodyState = "failed"`
  assignments are a different, unrelated vocabulary and were left as
  literals.
- `EfEvaSubmissionWorkStore.cs` — the same substitution across its terminal
  checks, the `is not (...)` unknown-state guard, the `ExecuteUpdateAsync`
  claim, the lease-authority check and the outcome switch. **Control flow
  unchanged**, including the `_ => Pending` catch-all in the outcome switch.
- `EfEvaSubmissionQueries.cs` — the `"completed"`/`"failed"` terminal-state
  comparison in `GetActivityAsync` now reads the constants.

`git diff origin/dev --stat`: 4 files, 62 insertions, 35 deletions.

All three files are now a literal-for-constant substitution and nothing
else. `const string` is a compile-time constant, so the emitted IL and the
persisted strings are identical to `origin/dev`'s; `RetryScheduled` still
persists as `pending`. No migration, no Razor/Pages/OperatorLabels touched,
no test file touched.

## Why / reuse

Core has no owning type for the full six-code persistence vocabulary (see
the `plan` doc). The codebase's per-store `ToCode`/`ParseState` convention
(`EfCaseAcceptanceStore`, `EfIntakeReceiptStore`, `EfAiWorkRequestStore`,
`EfApprovedMailboxStore`) doesn't fit, because three separate classes need
the same codes — so one small internal static class, not a per-store private
method, is the owner of the word list.

**Deliberately not built:** a method mapping the persisted word to Core's
`EvaSubmissionWorkState`. `EfVehicleLookupWorkStore.MapWorkState` already
owns that rule for this table; a second copy against a different Core enum
would have added a fourth copy of the very duplication this ticket exists to
remove. The first attempt did exactly that and it was withdrawn on review.
Whether the two enum mappings can share a shape is [[PLAT-056]].

## Build

`dotnet build ./Pegasus.slnx --configuration Release` — exit 0, `0 Warning(s)`,
`0 Error(s)`.

## Tests

Two focused runs covering every non-Browser test class in the repository
that touches `ExternalWorkItems` or `IEvaSubmissionQueries`, identified by
`grep -rln -E "ExternalWorkItems|IExternalWorkStore" tests/`.

Run 1 — `CustodyOutboxIntegrationTests`, `ServiceHealthPersistenceTests`,
`AutomaticVehicleLookupTests`, `ImageCaseCustodyIntegrationTests`,
`QdosAllocationRecoveryTests`, `VehicleLookupGapFillTests`,
`VehicleLookupBackfillTests`, `VehicleWorkflowTerminalTests`,
`CaseTaskArchivePersistenceTests`, `EvaSubmissionPersistenceTests`:
**Failed: 0, Passed: 101, Skipped: 1, Total: 102** (exit 0).

Run 2 — `AzureSqlRuntimeRoleMigrationTests`, `IntakeWebNegativeTests`,
`TypedCaseDataMigrationTests`, `OperationsWebTests`:
**Failed: 0, Passed: 37, Skipped: 0, Total: 37** (exit 0).

Combined: 138 passed, 0 failed, 1 skipped. The skip is
`CustodyOutboxIntegrationTests.AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource`,
skipped on `origin/dev` too.

**What this does and does not prove.** It is coverage of the substitution,
not the proof of behaviour preservation — that rests on the constants being
compile-time identical to the literals they replace. Per changed file:
`EfExternalWorkStore.cs` is exercised by eleven of the classes above;
`EfEvaSubmissionQueries.cs` by `ServiceHealthPersistenceTests` and
`OperationsWebTests`; `EfEvaSubmissionWorkStore.cs` by **nothing** —
`grep -rn "EfEvaSubmissionWorkStore" tests/` is empty, a gap that predates
this ticket and is raised as [[PLAT-057]].

`EvaSubmissionPersistenceTests` was cited as proof in the first version of
this report. That was wrong: it seeds `context.EvaSubmissions` only and
touches none of the changed classes. The citation is withdrawn.

## Commits

- `8a358ad4` — `refactor(infrastructure): unify external-work state
  literals under one owner (PLAT-053)` (first attempt).
- Round-2 remediation commit — withdraws the parse/format pair and the
  `EfEvaSubmissionWorkStore` restructure; see PR #613.

## Out-of-scope defects found

The same `ExternalWorkItems.State` literals remain in ten further
Infrastructure classes: `EfVehicleLookupWorkStore.cs` (49, 53, 57, 145 and
`MapWorkState`), `EfAutomaticEvaSubmissionStore.cs:95`,
`EfQueuedCustodyProcessor.cs` (43, 1049, 1076), `EfOperationsStore.cs`
(287, 460), `EfCaseWorkflowStore.cs` (1178-1184),
`EfCaseAcceptanceStore.cs:391`, `EfImageIntakeStore.cs` (212, 632),
`EfLinkedCaseReplacementStore.cs:214`, `EfVehicleWorkflowStore.cs`
(134, 903). The first version of this report named five of these and folded
the rest into "other external-work producers", understating the follow-up.

Not fixed here: this ticket's "Owns" list names exactly three files, and
EPIC-011's contract is one ticket per whole file with no cross-lane edits.
Raised as [[PLAT-056]].

## Risks / open questions

None outstanding. The consolidation is deliberately partial and the code now
says so at the one place a reader would be misled — the owner class's own
doc comment names PLAT-056 as the remaining work rather than claiming
completed ownership.
