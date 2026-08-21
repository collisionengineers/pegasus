# Plan

Two commits on `task/qdos26008-regressions`, shared with the other seven QDOS26008
fixes. `1a86f5db` fixed the projection; `db1055a3` adds cancel-on-unlink.

## Order mattered

1. **Consolidate the terminal taxonomy before adding to it.** `EvaHandoffStore` and
   `EfVehicleWorkflowStore` each kept their own copy of the terminal-state list, so a
   new state added only to `CaseLifecycleRules.IsTerminal` would have been silently
   non-terminal for EVA hand-off and for the vehicle-work sweep. Both now read Core, and
   `TerminalStateNames()` is derived from `IsTerminal` rather than restating it, so the
   two cannot drift by construction.
2. **Add `SourceEmailUnlinked`** to `CaseLifecycleState` and `CaseClosureOutcome` —
   both, because every other terminal outcome appears in both.
3. **Refuse it from the generic close**, in Core and in the store, exactly as
   `CreatedInError` is refused: the outcome belongs to the unlink action.
4. **Cancel inside the unlink's own transaction.** `ReverseLinkAsync`'s existing
   envelope already holds the case version, the edit lease, the terminal guard, replay
   protection and history writing, so the change is one conditional plus
   `CancelOnSourceUnlinkAsync`. Mirrors `EfLinkedCaseReplacementStore`, the codebase's
   pattern for a terminal outcome reached by a specific business action.
5. **Decide the rule once.** `IntakeReceipt.UnlinkCancelsCase` sits beside the other
   association derivations, so the warning the UI shows and the cancellation the store
   performs read the same rule.
6. **Warn through the shared dialog.** `_ReasonDialog` gains an optional
   `DialogConsequence` slot — the partial's own header always claimed it carried "a
   named requirement and consequence" and it did not. One slot serves every destructive
   dialog. It reuses the existing `.notice` class, so no new CSS.
7. **Label and copy.** `OperatorLabels.CaseStage`, and the sentence added to the closed
   necessary-copy list in `docs/design/README.md` in both places it appears.

## What research changed

A second defect was diagnosed — that the spawning email could not be unlinked at all —
and then **disproved** by a pre-existing test before the dead code path it motivated
could ship. `EfCaseAcceptanceStore.cs:332` writes an active manual association alongside
the accepted link. The helper written for that phantom defect was deleted, and the
cancel moved into the branch that actually runs.

## Deliberately not done

- **No new "next action" UI** — `Mail/Message.cshtml:444-459` already renders the case
  search-and-link form when no case is associated. Confirmed by reading the view.
- **No EF migration** — string columns.
- **No second close path** — nothing added to `CloseCase`.
- **`SourceEmailUnlinked` is not on the reopen bar**, so an accidental unlink is
  recoverable by a deliberate reopen with a reason.

## Acceptance

- Unlinking the accepted origin closes the case as `Cancelled — email unlinked`, the
  `CaseIntakeLinks` row survives, and the mail list stops naming the case. ✅
- A receipt relinked elsewhere does not cancel that other case. ✅
- The generic close refuses the outcome. ✅
- `TerminalStateNames()` and `IsTerminal` agree exactly. ✅
- The dialog sentence appears only when the unlink cancels. ✅ (view-level; live-checked
  in Phase 6)

## Evidence

- `dotnet build Pegasus.slnx --configuration Release` — 0 warnings, 0 errors.
- `Pegasus.Core.Tests` — 908 passed, 0 failed.
- `UnlinkingTheAcceptedOriginCancelsTheCaseAndKeepsItsLineage` — passed (38 s).
- Full integration suite: Phase 4, before the PR.

## Simplification pass

Recorded here once the whole branch diff is complete, before the PR.
