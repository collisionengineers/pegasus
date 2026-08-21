# Plan

Two defects, one branch (`task/qdos26008-regressions`, shared with the other seven
QDOS26008 fixes). The projection half is already committed; this is the cancel half.

## Order matters

1. **Consolidate the terminal taxonomy before adding to it.** Give
   `CaseLifecycleRules` a `TerminalStateNames` list beside `IsTerminal`, and have
   `EvaHandoffStore.IsTerminalWorkflow` and `EfVehicleWorkflowStore.EnqueueDueAsync`
   read it instead of keeping their own copies. Behaviour-preserving on its own, and it
   is what makes step 2 safe rather than a silent three-way divergence.
2. **Add `SourceEmailUnlinked`** to `CaseLifecycleState` and `CaseClosureOutcome` — both,
   because every other terminal outcome appears in both.
3. **Refuse it from the generic close.** `CaseLifecycleRules.ValidateClose` rejects it in
   the same shape it already rejects `CreatedInError`: this outcome is reached by the
   unlink action, never chosen from a Close dialog.
4. **Make the accepted origin reversible.** In `ReverseLinkAsync`, when the receipt has
   no manual association but an accepted `CaseIntakeLink` names `request.CaseId`, write
   an **inactive** `IntakeManualAssociation` row for it and close the case in the same
   transaction — `State`/`ClosureOutcome` = `SourceEmailUnlinked`, stop due work, bump
   version, clear the lease, add the workflow event. This mirrors
   `EfLinkedCaseReplacementStore.cs:220-221`, which is the codebase's existing pattern
   for a terminal outcome reached by a specific business action. The origin link itself
   is never deleted. Guard: refuse if the case is already terminal.
5. **Warn before the mutation.** `_ReasonDialog` gains an optional `DialogConsequence`
   slot rendered only when supplied — the partial's own header already claims it carries
   "a named requirement and consequence" and does not. One shared slot serves every
   destructive dialog instead of a bespoke warning on one page.
   `OnPostPrepareUnlinkCaseAsync` decides whether this unlink cancels and supplies
   `Unlinking this email cancels case <reference>.`
6. **Label and copy.** `OperatorLabels.CaseStage` gains `Cancelled — email unlinked`;
   `docs/design/README.md` gains the sentence as a fourth approved entry, in both places
   the closed list appears.

## Deliberately not done

- **No new "next action" UI.** `Mail/Message.cshtml:444-459` already renders the case
  search-and-link form when no case is associated; the dead end was the link never
  clearing. Confirmed by reading the view, not assumed.
- **No EF migration.** `State`/`ClosureOutcome` are `string?`/`HasMaxLength(40)`.
- **No second close path.** Nothing new is added to `CloseCase`.

## Acceptance

- Unlinking the spawning email closes the case as `Cancelled — email unlinked`, the
  accepted origin link survives, and the mail list stops naming the case.
- Unlinking a non-spawning email behaves exactly as today and leaves the case open.
- The generic close refuses the outcome; the case cannot be reopened.
- `IsTerminal` agrees at all three former call sites.
- The dialog sentence appears only when the unlink cancels.

## Simplification pass

Recorded here after the branch diff is complete, before the PR.
