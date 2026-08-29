# Plan — PLAT-053

> Corrected 2026-08-29 after adversarial verification of PR #613. The
> original plan chose a `ParseEvaSubmission`/`FormatEvaSubmission` pair that
> duplicated a pre-existing parse rule and quietly restructured
> `EfEvaSubmissionWorkStore`. Both were withdrawn; the sections below are the
> shipped plan, not the first one. Dispositions are at the foot.

## Search before you build

Checked `src/Pegasus.Core` for an existing owner of the persisted
`ExternalWorkItems.State` vocabulary (`pending`, `dispatching`, `queued`,
`processing`, `completed`, `failed`): no enum or constant set names these six
persisted codes. Core does own `EvaSubmissionWorkState`
(`Pending`/`RetryScheduled`/`Processing`/`Completed`/`Failed`) as the business
state for an EVA submission, but that is a narrower, already-mapped concept —
`RetryScheduled` collapses onto the persisted `pending` code, and Core has no
type for the three purely-persistence phases `dispatching`/`queued`/
`processing` that only Infrastructure's dispatch/lease machinery needs.

The codebase's existing convention for a *store-local* code mapping is a
private static `ToCode`/`ParseState` pair (`EfCaseAcceptanceStore.ToCode`,
`EfIntakeReceiptStore.ToCode`, `EfAiWorkRequestStore.ParseState`,
`EfApprovedMailboxStore.ParseState`). Those serve one store class each.

**Missed in the first pass, and the reason the first design was wrong:**
`EfVehicleLookupWorkStore.MapWorkState` (~line 422) is the closest analogue
in the repository — same `ExternalWorkItems` table, same six words, same
`AttemptCount > 0 → RetryScheduled` collapse, differing only in which Core
enum it returns (`VehicleLookupWorkState`). Any new parse method over this
vocabulary is a second copy of that rule, not a consolidation of it.

## Decision

The duplication this ticket exists to remove is the *word list*, and only the
word list. So the owner is one `internal static class`
(`ExternalWorkStatePersistence`) holding six `const string` fields and
nothing else.

No parse/format methods. Mapping the persisted word to a Core enum is a
different concept from spelling the word, it already has an owner
(`MapWorkState`), and a second copy of it against `EvaSubmissionWorkState`
would have made the duplication this ticket targets strictly worse. Whether
the two enum mappings can share one shape is [[PLAT-056]]'s call, not a
side effect of a constants extraction.

Consequences of holding to constants-only: every call site keeps the control
flow it had. `const string` fields are compile-time constants, so they are
legal in `is`/`or` patterns, in EF `Where` predicates and in
`ExecuteUpdateAsync().SetProperty(...)` — the substitution needs no
restructuring anywhere, and the emitted IL and the persisted strings are
unchanged.

## Steps

1. Add `ExternalWorkStatePersistence.cs` with the six constants.
2. Replace every `ExternalWorkItemEntity.State` literal in
   `EfExternalWorkStore.cs` with the constants; leave the two
   `Case.CustodyState = "failed"` sites alone (different vocabulary).
3. Replace the `State` literals in `EfEvaSubmissionWorkStore.cs` with the
   constants — comparisons, the `is not (...)` unknown-state guard, the
   `ExecuteUpdateAsync` claim and the outcome switch. Control flow untouched.
4. Replace the two terminal-state literals in
   `EfEvaSubmissionQueries.GetActivityAsync` with the constants.
5. Build Release. Run the focused filter over the classes that actually
   drive `ExternalWorkItems` (see below) — coverage of the substitution, not
   proof of behaviour preservation, which rests on the constants being
   compile-time identical to the literals they replace.
6. Commit, push. Do not touch the further copies elsewhere — out of this
   ticket's named "Owns" list; raised as [[PLAT-056]].

## Test coverage — what the focused run does and does not reach

Per changed file:

| File | Exercised by |
| --- | --- |
| `EfExternalWorkStore.cs` | `CustodyOutboxIntegrationTests`, `AutomaticVehicleLookupTests`, `ImageCaseCustodyIntegrationTests`, `QdosAllocationRecoveryTests`, `VehicleLookupBackfillTests`, `VehicleLookupGapFillTests`, `VehicleWorkflowTerminalTests`, `CaseTaskArchivePersistenceTests`, `IntakeWebNegativeTests`, `TypedCaseDataMigrationTests`, `AzureSqlRuntimeRoleMigrationTests` |
| `EfEvaSubmissionQueries.cs` | `ServiceHealthPersistenceTests`, `OperationsWebTests` |
| `EfEvaSubmissionWorkStore.cs` | **nothing** — `grep -rn "EfEvaSubmissionWorkStore" tests/` is empty |

`EvaSubmissionPersistenceTests` is **not** coverage for any changed file: it
seeds `context.EvaSubmissions` only and never touches `ExternalWorkItems`.
The first report cited it; that citation was wrong and is withdrawn.

The `EfEvaSubmissionWorkStore` gap is pre-existing and is raised as
[[PLAT-057]]. It is tolerable for *this* diff only because the shipped change
to that file is compile-time-identical substitution; it was not tolerable for
the first attempt, which restructured the class.

## Out-of-scope defects found (not fixed here)

The same `ExternalWorkItems.State` literals also appear in ten further
Infrastructure classes: `EfVehicleLookupWorkStore.cs`,
`EfAutomaticEvaSubmissionStore.cs`, `EfQueuedCustodyProcessor.cs`,
`EfOperationsStore.cs`, `EfCaseWorkflowStore.cs`, `EfCaseAcceptanceStore.cs`,
`EfImageIntakeStore.cs`, `EfLinkedCaseReplacementStore.cs`,
`EfVehicleWorkflowStore.cs` — with per-line references in [[PLAT-056]]. The
first version of this document said "and other external-work producers",
which understated the follow-up by five files; all ten are now named.

This ticket's "Owns" list named exactly three files, and EPIC-011's contract
is one ticket per whole file with no cross-lane edits.

## Simplification pass

Run 2026-08-29 over the branch diff, after the verification round. Findings
and dispositions are in the section below — the round-2 dispositions *are*
the simplification pass for this branch, since three of them
(single-caller abstraction, duplicated parse rule, inconsistent comparison)
are exactly the reuse/simplification lenses. Net effect: the diff shrank
from 81 insertions across 4 files to 44, and
`ExternalWorkStatePersistence.cs` from 38 lines to 19.
