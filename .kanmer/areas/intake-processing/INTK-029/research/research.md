# Why unlink did very little

The operator unlinked QDOS26008's spawning email, confirmed the dialog, and nothing
happened. Two separate defects sit behind that, not one.

## Defect 1 — a reversed association kept reporting its case (fixed)

`EfRetainedMailboxMessageStore` projected the mail list's case as
`linkedCase?.CaseId ?? allocationState?.CaseId`. The automatic allocation attempt still
names the case it created, so once the manual association was reversed the fallback put
the very link the operator had just removed straight back on the screen.

Fixed in `1a86f5db`: `IntakeAssociations.AllocationMayStandIn(receiptId)` refuses the
allocation fallback for a receipt whose association has been reversed
(`CurrentIntakeAssociations.cs`).

## Defect 2 — the spawning email cannot be unlinked at all

This is the one the operator actually hit, and it was not in the original diagnosis.

A receipt reaches a case by one of two routes, and they are not the same row:

| Route | Row written | Written by |
| --- | --- | --- |
| Its allocation created/accepted the case — the **spawning** email | `CaseIntakeLinks` (the accepted origin) | `EfCaseAcceptanceStore.cs:316` |
| A later related email matched to an existing case (MAIL-09) | `IntakeManualAssociations` | `EfIntakeMutationStore.AutoLinkAsync` |

`AutoLinkAsync` explicitly **refuses** a receipt that already has an accepted case link
("An accepted intake receipt cannot be associated automatically"), so the spawning
receipt has an accepted origin link and **no manual association**.

`IntakeReceipt.CurrentCaseId` (`IntakeContracts.cs:406`) resolves
`ManualAssociationVersion is null ? AcceptedCaseId : ManualLinkedCaseId`. For the
spawning receipt that yields the case — so:

1. `Mail/Message.cshtml:430` renders the **Unlink** button;
2. `OnPostPrepareUnlinkCaseAsync` passes its `binding.CurrentCaseId == caseId` guard,
   takes a case edit lease and shows the confirm dialog;
3. `ReverseLinkAsync` (`EfIntakeMutationStore.cs:364-371`) reads
   `receipt.ManualAssociation`, finds `null`, and throws
   `IntakeAssociationConflictException("The requested active intake-to-case association
   does not exist.")`.

The UI offers an action the store refuses. That is "unlink did very little": a lease
taken, a dialog confirmed, an error, and no change.

## What the fix must therefore do

Make the accepted origin reversible, and make reversing it cancel the case — which is
what the operator asked for and what the evidence says the route was missing.

**The origin link is not deleted.** Release validation requires that intake "preserves
reversible source associations and both origins". Writing an *inactive*
`IntakeManualAssociation` row for the spawning receipt makes `CurrentCaseId` resolve to
`null` through the precedence rule that already exists — `manualIsActive is null ?
accepted : (active ? manual : none)` — while `CaseIntakeLinks` survives untouched as
history. The Defect-1 projection fix then reports it as reversed with no further change.

## The dead end resolves itself

`Mail/Message.cshtml:444-459` already renders a case search-and-link form whenever no
case is associated. The operator saw a dead end only because the link never actually
cleared. No new "next action" UI is needed.

## Terminal states are written in three places

Adding a terminal state is only safe once this is fixed, because missing one copy makes
the new state silently non-terminal:

- `CaseLifecycleRules.IsTerminal` — `Lifecycle/CaseLifecycle.cs:393` (the owner)
- `EvaHandoffStore.IsTerminalWorkflow` — `:1018`
- `EfVehicleWorkflowStore.EnqueueDueAsync` — `:795` (a `string[]` for a LINQ query)

## Checked, not assumed

- `State` and `ClosureOutcome` are `string?` columns, `HasMaxLength(40)`
  (`CaseWorkflowModelConfiguration.cs:25,27`) — **no EF migration is needed** for a new
  enum value. The earlier plan's "lifecycle state plus a migration" was wrong.
- QDOS26008 itself could not be re-queried: the Azure test data was cleared earlier in
  this session. The diagnosis above is from the code path, and every citation is a read
  of the current source rather than a recollection.

## Operator decisions taken

- New terminal outcome, not `CreatedInError` (which requires the atomic corrected-
  principal replacement and refuses the generic close).
- State label `Cancelled — email unlinked`; dialog sentence
  `Unlinking this email cancels case QDOS26008.` This is a fourth entry on the closed
  necessary-copy list, so `docs/design/README.md` is amended in the same change.
