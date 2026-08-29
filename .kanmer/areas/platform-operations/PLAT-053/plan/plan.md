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
are exactly the reuse/simplification lenses.

Net effect, `git diff origin/dev --stat`: 62 insertions / 35 deletions across
4 files, down from 81 / 42; `ExternalWorkStatePersistence.cs` is 19 lines,
down from 38.

## Review findings — dispositions (round 2) — 2026-08-29

Adversarial verification of PR #613 (`VERDICT: needs-work`). All seven
findings below; none silenced. The verifier's scope-breach and
assertion-tampering checks came back clean and are not repeated here.

### [major] A fourth copy of the parse rule was added, not removed — `ParseEvaSubmission` duplicates `EfVehicleLookupWorkStore.MapWorkState`

**Disposition: fixed.** The finding is correct and is the most serious one:
in a ticket whose subject is "one list per concept", the first attempt added
a second copy of the `AttemptCount > 0 → RetryScheduled` parse rule. It was
not caught because the plan's search-before-build listed the four per-store
`ToCode`/`ParseState` helpers but not `MapWorkState`, the one analogue on
the same table.

Not folded together: `MapWorkState` returns `VehicleLookupWorkState` and the
EVA path needs `EvaSubmissionWorkState` — two Core enums, so one shared
method needs a generic or a mapping shim that no second concrete caller
earns today, and `EfVehicleLookupWorkStore.cs` is outside this ticket's
"Owns" list in any case. So the new copy was deleted rather than merged, and
the choice of shape is [[PLAT-056]]'s to make.

Both methods are gone; the class is now six constants and nothing else:

```diff
-internal static EvaSubmissionWorkState ParseEvaSubmission(
-    string value,
-    int attemptCount) => value switch
-    {
-        Pending when attemptCount > 0 => EvaSubmissionWorkState.RetryScheduled,
-        Pending or Dispatching or Queued => EvaSubmissionWorkState.Pending,
-        Processing => EvaSubmissionWorkState.Processing,
-        Completed => EvaSubmissionWorkState.Completed,
-        Failed => EvaSubmissionWorkState.Failed,
-        _ => throw new InvalidDataException(
-            $"The EVA submission work item has unknown state '{value}'.")
-    };
-
-internal static string FormatEvaSubmission(EvaSubmissionWorkState state) => state switch
-    {
-        EvaSubmissionWorkState.RetryScheduled => Pending,
-        EvaSubmissionWorkState.Completed => Completed,
-        EvaSubmissionWorkState.Failed => Failed,
-        _ => throw new ArgumentOutOfRangeException(nameof(state))
-    };
```

`grep -rn "ParseEvaSubmission\|FormatEvaSubmission" src/ tests/` → no source
hits.

### [major] The vocabulary stays literal in ~10 other Infrastructure files; the new class's XML doc asserts ownership it does not have

**Disposition: docstring fixed; remaining files deferred to [[PLAT-056]]
(created).**

The overclaiming doc comment was the honesty half of this finding and is
corrected — it now states the limit instead of asserting ownership:

```diff
-/// Owns the persisted <see cref="ExternalWorkItemEntity.State"/> vocabulary
-/// and its mapping to the Core EVA-submission work state.
+/// The persisted <see cref="ExternalWorkItemEntity.State"/> words. Stores
+/// compare and assign these constants instead of repeating the literals.
+///
+/// Not yet the vocabulary's only reader: the remaining Infrastructure stores
+/// on the same table still spell the words out, and folding them onto this
+/// class is PLAT-056.
```

The remaining ten files are **not** fixed here, against the verifier's stated
preference. Reason: this lane's binding brief and EPIC-011's contract both
say a ticket owns whole files and never edits a neighbour lane's — PLAT-053's
"Owns" list names exactly three. The ten span custody, image intake, vehicle
workflow and operations and reach into live wave-3 lanes; taking them would
be exactly the "while I'm here" absorption AGENTS.md rules 1 and 2 forbid,
and would collide with in-flight branches. [[PLAT-056]] carries all ten with
per-line references and the parse-rule decision.

### [major] The only semantically restructured file has zero test coverage, so the 33 passing tests do not prove behaviour preservation

**Disposition: fixed at the cause; residual gap deferred to [[PLAT-057]]
(created).**

The finding is correct on both halves. Rather than add tests to cover a
restructure this ticket should not have made, the restructure itself was
withdrawn (see the first finding), so the file's diff is now
compile-time-identical substitution with no control-flow change left to
cover.

The false citation is also corrected: `EvaSubmissionPersistenceTests` seeds
`context.EvaSubmissions` only and exercises none of the changed classes. It
is struck from the evidence in the plan and the post-implementation report.

The test run was widened from 3 classes to 14 — every non-Browser test class
in the repository that touches `ExternalWorkItems` or `IEvaSubmissionQueries`,
found by `grep -rln -E "ExternalWorkItems|IExternalWorkStore" tests/`. Real
numbers, two commands, both exit 0: **138 passed, 0 failed, 1 skipped**
(101/0/1 and 37/0/0). The seven classes the verifier named as "not run" —
`AutomaticVehicleLookupTests`, `ImageCaseCustodyIntegrationTests`,
`QdosAllocationRecoveryTests`, `VehicleLookupGapFillTests`,
`VehicleLookupBackfillTests`, `VehicleWorkflowTerminalTests`,
`CaseTaskArchivePersistenceTests` — are all in the run now.

`EfEvaSubmissionWorkStore` still has no test of its own. That gap predates
this ticket (`grep` was empty on `origin/dev` too) and is [[PLAT-057]].

### [minor] Both new mapping methods have one caller each; `ParseEvaSubmission` returns a distinction its only caller cannot observe

**Disposition: fixed.** Subsumed by the first finding — both methods deleted.
The verifier's reading was right: the sole caller tested only
`is Completed or Failed` and `== Processing`, so the `RetryScheduled` branch
was unobservable dead semantics. The six constants, which do have many
callers, stay.

### [minor] Adjacent lines in `ClaimProcessingAsync` compare the same field two different ways

**Disposition: fixed.** With the parse call gone, all four guards in
`ClaimProcessingAsync` compare `work.State` to a constant. The trap the
verifier identified — that "tidying" line 59 to `state == …Pending` would
silently stop honouring the not-yet-due guard for retried submissions — no
longer exists, because there is no `state` local to tidy it into. No comment
was added: there is nothing left to warn about.

### [minor] A behaviour-changing edge was converted from a silent default to a throw, unreachable only because of a guard 45 lines away

**Disposition: fixed — previous behaviour restored.** The verifier offered
"record the dependency at the throw, or restore the previous behaviour"; the
second is better, since the dependency only existed because of a restructure
this ticket no longer makes. The original catch-all is back, constants-only:

```diff
 work.State = state switch
 {
-    EvaSubmissionWorkState.Completed => "completed",
-    EvaSubmissionWorkState.Failed => "failed",
-    _ => "pending"
+    EvaSubmissionWorkState.Completed => ExternalWorkStatePersistence.Completed,
+    EvaSubmissionWorkState.Failed => ExternalWorkStatePersistence.Failed,
+    _ => ExternalWorkStatePersistence.Pending
 };
```

The `is not (...)` unknown-state guard that the first attempt deleted from
`ClaimProcessingAsync` is likewise restored in place, so the file's throw
behaviour matches `origin/dev` exactly.

### [minor] The reported out-of-scope list is honest but incomplete — five further files folded into "other external-work producers"

**Disposition: fixed.** All ten are now named with line references, in the
plan's out-of-scope section, in the post-implementation report, and in
[[PLAT-056]]'s body: the five originally named plus
`EfCaseAcceptanceStore.cs:391`, `EfImageIntakeStore.cs:212,632`,
`EfLinkedCaseReplacementStore.cs:214`, `EfVehicleWorkflowStore.cs:134,903`.

### Honesty corrections to the record

Two overclaims the verifier identified, both now corrected at source rather
than only in prose:

1. *"Mechanical literal-to-constant substitution with byte-identical
   persisted strings."* This was false for `EfEvaSubmissionWorkStore.cs`,
   which had been restructured. It is **now true of all three files** because
   the restructure was withdrawn — the claim was made accurate rather than
   softened.
2. *"Run the focused test filter to prove behaviour is unchanged",* citing
   `EvaSubmissionPersistenceTests`. Withdrawn. Behaviour preservation now
   rests on the constants being compile-time identical to the literals they
   replace; the test run is stated as coverage of the substitution, and the
   per-file coverage table names exactly which class reaches which file, and
   which file nothing reaches.
